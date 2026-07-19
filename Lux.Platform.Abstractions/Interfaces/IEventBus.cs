using System;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IEventBus
{
    void Publish<TEvent>(TEvent message) where TEvent : class;
    void Subscribe<TEvent>(object recipient, Action<TEvent> action) where TEvent : class;
    void Unsubscribe<TEvent>(object recipient) where TEvent : class;
    void UnsubscribeAll(object recipient);
}
