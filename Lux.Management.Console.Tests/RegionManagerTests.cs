using System;
using System.Windows.Controls;
using Lux.Management.Console.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lux.Management.Console.Tests;

public class RegionManagerTests
{
    private class DummyView : UserControl { }

    [Fact]
    public void RegisterRegion_ThrowsIfTargetNotContentControl()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var regionManager = new RegionManager(serviceProvider);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => regionManager.RegisterRegion("MainRegion", new object()));
    }

    [Fact]
    public void NavigateTo_ViewInstance_SetsContent()
    {
        Exception? exception = null;
        var t = new System.Threading.Thread(() =>
        {
            try
            {
                // Arrange
                var services = new ServiceCollection();
                var serviceProvider = services.BuildServiceProvider();
                var regionManager = new RegionManager(serviceProvider);
                var contentControl = new ContentControl();
                var view = new DummyView();

                regionManager.RegisterRegion("MainRegion", contentControl);

                // Act
                regionManager.NavigateTo("MainRegion", view);

                // Assert
                Assert.Same(view, contentControl.Content);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();

        if (exception != null) throw exception;
    }

    [Fact]
    public void NavigateTo_GenericType_ResolvesAndSetsContent()
    {
        Exception? exception = null;
        var t = new System.Threading.Thread(() =>
        {
            try
            {
                // Arrange
                var services = new ServiceCollection();
                services.AddTransient<DummyView>();
                var serviceProvider = services.BuildServiceProvider();
                var regionManager = new RegionManager(serviceProvider);
                var contentControl = new ContentControl();

                regionManager.RegisterRegion("MainRegion", contentControl);

                // Act
                regionManager.NavigateTo<DummyView>("MainRegion");

                // Assert
                Assert.IsType<DummyView>(contentControl.Content);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();

        if (exception != null) throw exception;
    }
}
