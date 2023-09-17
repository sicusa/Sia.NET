namespace Sia;

using System;

public static class HOEvents
{
    public class Cancel<TEvent> : SingletonEvent<Cancel<TEvent>>, ICancelEvent
        where TEvent : ICancellableEvent
    {
        public Type InnerEventType => typeof(TEvent);
    }

    public class Pause<TEvent> : SingletonEvent<Pause<TEvent>>, IPauseEvent
        where TEvent : IPausableEvent
    {
        public Type InnerEventType => typeof(TEvent);
    }

    public class Resume<TEvent> : SingletonEvent<Resume<TEvent>>, IResumeEvent
        where TEvent : IPausableEvent
    {
        public Type InnerEventType => typeof(TEvent);
    }

    public readonly record struct Revert<TEvent>(TEvent Value) : IRevertEvent
        where TEvent : IRevertableEvent
    {
        public Type InnerEventType => typeof(TEvent);
    }
}