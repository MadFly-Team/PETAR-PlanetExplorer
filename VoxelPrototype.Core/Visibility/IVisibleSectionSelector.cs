namespace VoxelPrototype.Core.Visibility;

public interface IVisibleSectionSelector
{
    VisibleSectionSet BuildVisibleSet(VisibilityContext context);
}
