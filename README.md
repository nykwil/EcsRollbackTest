# EcsRollbackTest

This currently doesn't work as CopyAndReplaceEntitiesFrom causes various errors. Tracked here: https://fogbugz.unity3d.com/default.asp?1425370_sl2paqrmfhihfo0s

## Assets/Tests Folder (start here)
This is where the rollback and bootstrap code lives.

### Boostrap.cs
Builds the worlds and create update system

### CustomUpdateSystem.cs
This is where the ticking and rollback occurs.

## Assets/EcsWar Folder 
This is minimal components and systems for a shooting game.

