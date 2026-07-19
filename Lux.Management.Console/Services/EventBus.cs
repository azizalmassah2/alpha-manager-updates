using System;
using CommunityToolkit.Mvvm.Messaging;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Services;

public class EventBus : IEventBus
{
    public void Publish<TEvent>(TEvent message) where TEvent : class
    {
        WeakReferenceMessenger.Default.Send(message);
    }

    public void Subscribe<TEvent>(object recipient, Action<TEvent> action) where TEvent : class
    {
        WeakReferenceMessenger.Default.Register<TEvent>(recipient, (r, m) => action(m));
    }

    public void Unsubscribe<TEvent>(object recipient) where TEvent : class
    {
        WeakReferenceMessenger.Default.Unregister<TEvent>(recipient);
    }

    public void UnsubscribeAll(object recipient)
    {
        WeakReferenceMessenger.Default.UnregisterAll(recipient);
    }
}
