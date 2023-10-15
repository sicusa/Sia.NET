namespace Sia;

using System.Runtime.CompilerServices;

public class Hierarchy<TNodeEntity> : ViewBase<TypeUnion<Node>>
    where TNodeEntity : INodeEntity
{
    private readonly Stack<HashSet<EntityRef>> _childrenPool = new();

    public Hierarchy()
    {
        EntityUtility.CheckComponent<TNodeEntity, Node>();
    }

    public override void OnInitialize(World world)
    {
        base.OnInitialize(world);
        world.Dispatcher.Listen<Node.SetParent>(OnNodeParentChanged);
    }

    public override void OnUninitialize(World world)
    {
        world.Dispatcher.Unlisten<Node.SetParent>(OnNodeParentChanged);
        base.OnUninitialize(world);
    }

    protected override void OnEntityAdded(in EntityRef entity)
    {
        ref var node = ref entity.Get<Node>();
        var parent = node.Parent;
        if (parent != null) {
            AddToParent(entity, parent.Value);
        }
    }

    protected override void OnEntityRemoved(in EntityRef entity)
    {
        ref var node = ref entity.Get<Node>();

        var parent = node.Parent;
        if (parent != null) {
            RemoveFromParent(entity, parent.Value);
        }

        var children = node._children;
        if (children != null) {
            foreach (var child in children) {
                child.Dispose();
            }
            children.Clear();
            _childrenPool.Push(children);
        }
    }

    private bool OnNodeParentChanged(in EntityRef entity, in Node.SetParent e)
    {
        ref var node = ref entity.Get<Node>();

        var previousParent = node.PreviousParent;
        if (previousParent != null) {
            RemoveFromParent(entity, previousParent.Value);
        }

        var parent = node.Parent;
        if (parent != null) {
            AddToParent(entity, parent.Value);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToParent(in EntityRef entity, in EntityRef parent)
    {
        ref var parentNode = ref parent.Get<Node>();
        ref var children = ref parentNode._children;
        children ??= _childrenPool.TryPop(out var pooled) ? pooled : new();
        children.Add(entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFromParent(in EntityRef entity, in EntityRef parent)
    {
        ref var parentNode = ref parent.Get<Node>();
        ref var children = ref parentNode._children;
        children!.Remove(entity);

        if (children.Count == 0) {
            _childrenPool.Push(children);
            children = null;
        }
    }
}