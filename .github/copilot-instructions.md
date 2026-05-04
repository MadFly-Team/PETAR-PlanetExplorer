# Copilot Instructions

## General Guidelines
- Continue execution through all plan steps without pausing for interim status-only messages.
- Use a measurement-driven optimization workflow for performance work:
  - Profile first to identify hotspots.
  - Prefer existing benchmarks or tests as measurement artifacts.
  - Establish a baseline before optimizing.
  - Compare before-and-after results to validate improvements.

## Project Guidelines
- For the Minecraft-style voxel prototype, create a new project instead of implementing it as an in-game module.
- Use a height limit of 48 instead of 32 when adjusting the voxel landscape vertical normalization.
- Place landed oil rigs on the impacted voxel center at that cube's top height so they remain visible.

### Visual Effects
- Treat bubble dimensions, limits, and movement heights as world units (not cube counts) for Venus bubble effects; target visible count is 2–5 bubbles at a time.

### Voxel Renderer Performance
- Prioritize this architecture order:
  - Regenerate only newly entered chunks.
  - Mesh only dirty chunks.
  - Keep chunk meshes resident in GPU buffers.
  - Move generation and meshing off the main thread.
  - Use greedy meshing or another face-reduction strategy.
  - Apply stronger culling before LOD (level-of-detail) processing.zero height