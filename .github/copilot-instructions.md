# Copilot Instructions
on
## General Guidelines
- When executing an approved plan, continue through all plan steps end-to-end without pausing for interim status-only messages, requesting intermediate input, or awaiting confirmations, unless blocked by a real failure.
- Treat issues likely caused by the most recent change as regressions: investigate and validate them first before shifting focus to performance optimization.
- Use a measurement-driven optimization workflow for performance work:
  - Profile first to identify hotspots.
  - Prefer existing benchmarks or tests as measurement artifacts.
  - Establish a baseline before optimizing.
  - Compare before-and-after results to validate improvements.

## Project Guidelines
- For the Minecraft-style voxel prototype, create a new project instead of implementing it as an in-game module.
- Use a height limit of 48 instead of 32 when adjusting the voxel landscape vertical normalization.
- Place landed oil rigs on the impacted voxel center at that cube's top height so they remain visible.

### Debug Traffic
- Use the stored road spline/path for the debug traffic car and follow it directly, including through intermediate towns.
- Derive traffic direction from the start and end points of the current road section rather than local averaged heading samples.
- Treat the debug car's visual/model facing as correct; do not assume heading-axis errors are causing issues.
- Focus on path-following and steering behavior when debugging: eliminate route/movement fighting (zig-zagging) rather than changing the model's heading axis.
- When the debug traffic car is enabled, bypass complex route validation and destination-oriented steering; prioritize predictable path replay.
- Apply steering fixes to prevent oscillation: use lookahead-based steering, project targets onto the path, smooth or clamp angular corrections, reduce aggressive reorientation, and disable reactive re-pathing during replay.

### Visual Effects
- Treat bubble dimensions, limits, and movement heights as world units (not cube counts) for Venus bubble effects; target visible count is 2–5 bubbles at a time.

### Voxel Renderer Performance
- For PETAR-PlanetExplorer voxel renderer optimization, prioritize this architecture order:
  - Invalidate only dirty chunks (dirty-chunk invalidation).
  - Regenerate/mesh only newly entered or dirty chunks.
  - Keep chunk meshes resident in GPU buffers.
  - Move generation and meshing off the main thread.
  - Add greedy meshing or another face-reduction strategy.
  - Apply stronger culling before LOD (level-of-detail) processing.ed