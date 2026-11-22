namespace MiningOperation.Asteroids
{
    public enum MOBakeRebuildMode
    {
        // Detect manual Transform/component/child/material changes or explicit lock;
        // skip modified objects, update unchanged meshes, create missing objects.
        PreserveEdited = 0,
        // Replace generated meshes for all matching objects; preserve root Transform,
        // components, custom children and modified material assignments.
        UpdateGeometry = 1,
        // Generate a fresh field snapshot, remove the entire baked hierarchy and
        // build it from scratch. Manual GameObject changes will be lost.
        RebuildEntireField = 2
    }}
