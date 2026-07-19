using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Lux.Management.Console.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Lux.Management.Console.Navigation;

public class RegionManager : IRegionManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ContentControl> _regions = new();

    public RegionManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void RegisterRegion(string regionName, object regionTarget)
    {
        if (regionTarget is ContentControl contentControl)
        {
            _regions[regionName] = contentControl;
        }
        else
        {
            throw new ArgumentException("Region target must be a ContentControl", nameof(regionTarget));
        }
    }

    public void NavigateTo(string regionName, object view)
    {
        if (_regions.TryGetValue(regionName, out var contentControl))
        {
            contentControl.Content = view;
        }
        else
        {
            throw new KeyNotFoundException($"Region '{regionName}' is not registered.");
        }
    }

    public void NavigateTo<TView>(string regionName) where TView : class
    {
        var view = _serviceProvider.GetRequiredService<TView>();
        NavigateTo(regionName, view);
    }

    public void ClearRegion(string regionName)
    {
        if (_regions.TryGetValue(regionName, out var contentControl))
        {
            contentControl.Content = null;
        }
    }
}
