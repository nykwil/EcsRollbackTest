using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Serialization
{
    public static partial class SerializeUtility
    {
        public static unsafe void SerializeWorldState(EntityManager entityManager, BinaryWriter writer)
        {
            writer.Write(CurrentFileFormatVersion);
            var entityComponentStore = entityManager.EntityComponentStore;

            EntityArchetype[] archetypeArray = new EntityArchetype[entityComponentStore->m_Archetypes.Length];
            for (var i = 0; i != entityComponentStore->m_Archetypes.Length; i++)
                archetypeArray[i] = new EntityArchetype { Archetype = entityComponentStore->m_Archetypes.Ptr[i] };

            var typeHashes = new NativeHashMap<ulong, int>(1024, Allocator.Temp);
            int totalChunkCount = 0;
            foreach (var archetype in archetypeArray)
            {
                for (int iType = 0; iType < archetype.Archetype->TypesCount; ++iType)
                {
                    var typeIndex = archetype.Archetype->Types[iType].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var hash = typeInfo.StableTypeHash;
                    if ((typeInfo.TypeIndex & TypeManager.ManagedComponentTypeFlag) != 0)
                        throw new ArgumentException($"Managed (class) component type '{TypeManager.GetType(typeInfo.TypeIndex)}' is not serializable");
                    typeHashes.TryAdd(hash, 0);
                }
                totalChunkCount += archetype.Archetype->Chunks.Count;
            }
            var typeHashSet = typeHashes.GetKeyArray(Allocator.Temp);

            writer.Write(typeHashSet.Length);
            foreach (ulong hash in typeHashSet)
            {
                writer.Write(hash);
            }

            var typeHashToIndexMap = new NativeHashMap<ulong, int>(typeHashSet.Length, Allocator.Temp);
            for (int i = 0; i < typeHashes.Length; ++i)
            {
                typeHashToIndexMap.TryAdd(typeHashSet[i], i);
            }

            WriteArchetypes(writer, archetypeArray, typeHashToIndexMap);

            entityManager.EntityComponentStore->SerializeWorldState(writer);
            entityManager.ManagedComponentStore.SerializeWorldState(writer);

            var bufferPatches = new NativeList<BufferPatchRecord>(128, Allocator.Temp);
            writer.Write(totalChunkCount);

            var tempChunk = (Chunk*)UnsafeUtility.Malloc(Chunk.kChunkSize, 16, Allocator.Temp);

            for (int archetypeIndex = 0; archetypeIndex < archetypeArray.Length; ++archetypeIndex)
            {
                var archetype = archetypeArray[archetypeIndex].Archetype;
                for (var ci = 0; ci < archetype->Chunks.Count; ++ci)
                {
                    var chunk = archetype->Chunks.p[ci];
                    bufferPatches.Clear();

                    UnsafeUtility.MemCpy(tempChunk, chunk, Chunk.kChunkSize);

                    // Prevent patching from touching buffers allocated memory
                    BufferHeader.PatchAfterCloningChunk(tempChunk);

                    FillPatchRecordsForChunk(chunk, bufferPatches);

                    ClearChunkHeaderComponents(tempChunk);
                    ChunkDataUtility.MemsetUnusedChunkData(tempChunk, 0);
                    tempChunk->Archetype = (Archetype*)archetypeIndex;

                    if (archetype->NumManagedArrays != 0)
                    {
                        throw new ArgumentException("Serialization of GameObject components is not supported for pure entity scenes");
                    }

                    writer.WriteBytes(tempChunk, Chunk.kChunkSize);

                    int* chunkSharedComponentIndices = stackalloc int[archetype->NumSharedComponents];
                    chunk->SharedComponentValues.CopyTo(chunkSharedComponentIndices, 0, archetype->NumSharedComponents);
                    writer.WriteBytes(chunkSharedComponentIndices, sizeof(int) * archetype->NumSharedComponents);

                    writer.Write(bufferPatches.Length);

                    if (bufferPatches.Length > 0)
                    {
                        writer.WriteList(bufferPatches);

                        // Write heap backed data for each required patch.
                        // TODO: PERF: Investigate static-only deserialization could manage one block and mark in pointers somehow that they are not indiviual
                        for (int i = 0; i < bufferPatches.Length; ++i)
                        {
                            var patch = bufferPatches[i];
                            var header = (BufferHeader*)OffsetFromPointer(tempChunk->Buffer, patch.ChunkOffset);
                            writer.WriteBytes(header->Pointer, patch.AllocSizeBytes);
                            BufferHeader.Destroy(header);
                        }
                    }
                }
            }

            bufferPatches.Dispose();
            UnsafeUtility.Free(tempChunk, Allocator.Temp);

            typeHashes.Dispose();
            typeHashSet.Dispose();
            typeHashToIndexMap.Dispose();
        }

        public static unsafe void DeserializeWorldState(ExclusiveEntityTransaction manager, BinaryReader reader)
        {
            if (manager.EntityComponentStore->CountEntities() != 0)
            {
                throw new ArgumentException(
                    $"DeserializeWorldState can only be used on completely empty EntityManager. Please create a new empty World and use EntityManager.MoveEntitiesFrom to move the loaded entities into the destination world instead.");
            }
            int storedVersion = reader.ReadInt();
            if (storedVersion != CurrentFileFormatVersion)
            {
                throw new ArgumentException(
                    $"Attempting to read a entity scene stored in an old file format version (stored version : {storedVersion}, current version : {CurrentFileFormatVersion})");
            }

            var types = ReadTypeArray(reader);
            int totalEntityCount;
            var archetypeChanges = manager.EntityComponentStore->BeginArchetypeChangeTracking();

            var archetypes = ReadArchetypes(reader, types, manager, out totalEntityCount);

            manager.EntityComponentStore->DeserializeWorldState(reader);
            manager.ManagedComponentStore.DeserializeWorldState(reader);

            var changedArchetypes = manager.EntityComponentStore->EndArchetypeChangeTracking(archetypeChanges);
            manager.EntityQueryManager.AddAdditionalArchetypes(changedArchetypes);

            int totalChunkCount = reader.ReadInt();
            var chunksWithMetaChunkEntities = new NativeList<ArchetypeChunk>(totalChunkCount, Allocator.Temp);

            for (int i = 0; i < totalChunkCount; ++i)
            {
                var chunk = (Chunk*)UnsafeUtility.Malloc(Chunk.kChunkSize, 64, Allocator.Persistent);
                reader.ReadBytes(chunk, Chunk.kChunkSize);

                var archetype = chunk->Archetype = archetypes[(int)chunk->Archetype].Archetype;

                int* chunkSharedComponentIndices = stackalloc int[archetype->NumSharedComponents];
                reader.ReadBytes(chunkSharedComponentIndices, sizeof(int) * archetype->NumSharedComponents);

                // Allocate additional heap memory for buffers that have overflown into the heap, and read their data.
                int bufferAllocationCount = reader.ReadInt();
                if (bufferAllocationCount > 0)
                {
                    var bufferPatches = new NativeArray<BufferPatchRecord>(bufferAllocationCount, Allocator.Temp);
                    reader.ReadArray(bufferPatches, bufferPatches.Length);

                    // TODO: PERF: Batch malloc interface.
                    for (int pi = 0; pi < bufferAllocationCount; ++pi)
                    {
                        var target = (BufferHeader*)OffsetFromPointer(chunk->Buffer, bufferPatches[pi].ChunkOffset);

                        // TODO: Alignment
                        target->Pointer = (byte*)UnsafeUtility.Malloc(bufferPatches[pi].AllocSizeBytes, 8, Allocator.Persistent);

                        reader.ReadBytes(target->Pointer, bufferPatches[pi].AllocSizeBytes);
                    }

                    bufferPatches.Dispose();
                }

                manager.EntityComponentStore->AddExistingChunk(chunk, chunkSharedComponentIndices);

                if (chunk->metaChunkEntity != Entity.Null)
                {
                    chunksWithMetaChunkEntities.Add(new ArchetypeChunk(chunk, manager.EntityComponentStore));
                }
            }

            for (int i = 0; i < chunksWithMetaChunkEntities.Length; ++i)
            {
                var chunk = chunksWithMetaChunkEntities[i].m_Chunk;
                manager.SetComponentData(chunk->metaChunkEntity, new ChunkHeader { ArchetypeChunk = chunksWithMetaChunkEntities[i] });
            }

            chunksWithMetaChunkEntities.Dispose();
            archetypes.Dispose();
            types.Dispose();
            manager.EntityComponentStore->ManagedChangesTracker.Reset();
        }

        public static unsafe void AddSharedComponentAtIndex<T>(EntityManager manager, int sharedComponentIndex, T sharedComponentData) where T : struct
        {
            manager.ManagedComponentStore.AddAtIndex(sharedComponentIndex, sharedComponentData);
        }
    }
}

namespace Unity.Entities
{
    using Serialization;

    internal unsafe partial class ManagedComponentStore
    {
        internal unsafe void SerializeWorldState(BinaryWriter writer)
        {
            BinaryWriterExtensions.Write(writer, m_SharedComponentData.Count);
            writer.WriteBytes(m_SharedComponentInfo.Ptr, 4 * 4 * m_SharedComponentInfo.Length);
            BinaryWriterExtensions.Write(writer, m_FreeListIndex);
        }

        internal unsafe void DeserializeWorldState(BinaryReader reader)
        {
            int sharedComponentCount = BinaryReaderExtensions.ReadInt(reader);
            m_SharedComponentData.Clear();
            m_SharedComponentData.AddRange(new object[sharedComponentCount]);
            m_SharedComponentInfo.Resize<SharedComponentInfo>(sharedComponentCount);
            reader.ReadBytes(m_SharedComponentInfo.Ptr, 4 * 4 * m_SharedComponentInfo.Length);

            m_FreeListIndex = BinaryReaderExtensions.ReadInt(reader);
        }

        internal unsafe void AddAtIndex<T>(int sharedComponentIndex, T sharedComponentData) where T : struct
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int hashCode = TypeManager.GetHashCode<T>(ref sharedComponentData);
            m_HashLookup.Add(hashCode, sharedComponentIndex);
            m_SharedComponentData[sharedComponentIndex] = sharedComponentData;
            if (SharedComponentInfoPtr[sharedComponentIndex].ComponentType != typeIndex) throw new ArgumentException($"sharedtype {typeIndex} did not match type before serialization {SharedComponentInfoPtr[sharedComponentIndex].ComponentType}");
        }
    }

    internal unsafe partial struct EntityComponentStore
    {
        struct EntityIndexRecord { public int VersionByEntity, IndexInChunk; }

        internal unsafe void SerializeWorldState(BinaryWriter writer)
        {
            BinaryWriterExtensions.Write(writer, m_EntitiesCapacity);
            EntityIndexRecord* indices = (EntityIndexRecord*)UnsafeUtility.Malloc(sizeof(EntityIndexRecord) * m_EntitiesCapacity, 8, Allocator.Temp);
            for (int i = 0; i != m_EntitiesCapacity; i++)
            {
                indices[i].VersionByEntity = m_VersionByEntity[i];
                indices[i].IndexInChunk = m_EntityInChunkByEntity[i].IndexInChunk;
            }
            writer.WriteBytes(indices, sizeof(EntityIndexRecord) * m_EntitiesCapacity);
            UnsafeUtility.Free(indices, Allocator.Temp);
            BinaryWriterExtensions.Write(writer, m_NextFreeEntityIndex);
        }

        internal unsafe void DeserializeWorldState(BinaryReader reader)
        {
            EnsureCapacity(BinaryReaderExtensions.ReadInt(reader));
            EntityIndexRecord* indices = (EntityIndexRecord*)UnsafeUtility.Malloc(sizeof(EntityIndexRecord) * m_EntitiesCapacity, 8, Allocator.Temp);
            reader.ReadBytes(indices, sizeof(EntityIndexRecord) * m_EntitiesCapacity);
            for (int i = 0; i != m_EntitiesCapacity; i++)
            {
                m_VersionByEntity[i] = indices[i].VersionByEntity;
                m_EntityInChunkByEntity[i].IndexInChunk = indices[i].IndexInChunk;
            }
            UnsafeUtility.Free(indices, Allocator.Temp);
            m_NextFreeEntityIndex = BinaryReaderExtensions.ReadInt(reader);
        }
    }
}
