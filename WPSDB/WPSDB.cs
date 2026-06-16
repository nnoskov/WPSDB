using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;


using Newtonsoft.Json.Linq;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using VisualComponents.Create3D;
using VisualComponents.UX.Shared;

namespace WPSDB
{
  [Export(typeof(IPlugin))]
  public class WPSDB : IPlugin
  {
    const string LAYOUT_ITEM_NAME = "KWS_";
    const string DEFAULT_WPSDF_PATH = @"C:\Users\Public\Documents\Delfoi\WPS\WPS_DataFile.wpsdf";

    [Import]
    private Lazy<IApplication> _app = null;

    [ImportingConstructor]
    public WPSDB([Import(typeof(IEventAggregator))] IEventAggregator eventAggregator)
    {
      eventAggregator.Subscribe(this);
    }



    void IPlugin.Exit()
    {

    }


    void IPlugin.Initialize()
    {
      IApplication app = _app.Value;
      app.LayoutSaving += AppLayoutSaving;
      app.LayoutSaved += AppLayoutSaved;
      app.LayoutLoading += AppLayoutLoading;
      app.LayoutLoaded += AppLayoutLoaded;
    }

    private void AppLayoutLoaded(object sender, LayoutLoadedEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      ms.AppendMessage("Layout Loaded", MessageLevel.Warning);
    }

    private void AppLayoutLoading(object sender, LayoutLoadingEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      ms.AppendMessage("Layout Load Started", MessageLevel.Warning);
    }

    private void AppLayoutSaved(object sender, LayoutSavedEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      ms.AppendMessage("Layout Saving Finished", MessageLevel.Warning);
    }

    private bool LayoutHasRobots(ISimWorld world)
    {
      return world.Components.Any(comp => comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController));
    }

    private void AppLayoutSaving(object sender, LayoutSavingEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      Dictionary<string, string> robSettings = new Dictionary<string, string>();

      IApplication app = _app.Value;
      ISimWorld world = app.World;

      if (LayoutHasRobots(world))
      {
        if (File.Exists(DEFAULT_WPSDF_PATH))
        {
          ms.AppendMessage($"Default WPSDF file found at: {DEFAULT_WPSDF_PATH}", MessageLevel.Warning);
        }
        else
        {
          File.Create(DEFAULT_WPSDF_PATH).Close(); // Создаём пустой файл, если его нет
          ms.AppendMessage($"Default WPSDF was creted to path: {DEFAULT_WPSDF_PATH}", MessageLevel.Warning);
        }
      }

      foreach (var comp in world.Components)
      {
        if (comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController))
        {
          IProperty ropSettingsProp = comp.Properties.FirstOrDefault(p => p.Name == "RobotSettings");
          if (ropSettingsProp != null)
          {
            string robName = comp.Name;
            string wpsItemName = LAYOUT_ITEM_NAME + robName;
            string settingsValue = ropSettingsProp.Value?.ToString() ?? "{}";

            try
            {
              // Парсим JSON-строку в JObject
              JObject jWpsFilePath = JObject.Parse(settingsValue);
              // Получаем значение WpsFilePath
              string wpsFilePath = jWpsFilePath["WpsFilePath"]?.ToString() ?? "No WpsFilePath";

              ms.AppendMessage($"Processing component '{comp.Name}' with WpsFilePath: {wpsFilePath}", MessageLevel.Warning);

              // Проверяем существование файла
              if (!string.IsNullOrEmpty(wpsFilePath) && File.Exists(wpsFilePath))
              {
                // Читаем содержимое файла WPSDF
                string wpsdfContent = File.ReadAllText(wpsFilePath);
                // Парсим содержимое WPSDF в JObject
                JObject wpsdfObject = JObject.Parse(wpsdfContent);
                // Сериализуем объект обратно в строку
                string serializedData = wpsdfObject.ToString();

                // Проверяем существование элемента
                ILayoutPropertyList layoutPropertiesList = null;

                // Проверяем, существует ли уже элемент с таким именем
                var existingItem = world.LayoutItems.FirstOrDefault(li => li.Name == wpsItemName);

                if (existingItem != null)
                {
                  // Если существует, используем его
                  layoutPropertiesList = existingItem as ILayoutPropertyList;
                  if (layoutPropertiesList == null)
                  {
                    // Если элемент существует, но не того типа, удаляем и создаём заново
                    world.DeleteLayoutItem(existingItem);
                    layoutPropertiesList = world.CreateLayoutItem<ILayoutPropertyList>(wpsItemName);
                  }
                  else
                  {
                    ms.AppendMessage($"Using existing layout item: '{wpsItemName}'", MessageLevel.Warning);
                  }
                }
                else
                {
                  // Если не существует, создаём новый
                  layoutPropertiesList = world.CreateLayoutItem<ILayoutPropertyList>(wpsItemName);
                  ms.AppendMessage($"Created new layout item: '{wpsItemName}'", MessageLevel.Warning);
                }

                if (layoutPropertiesList != null)
                {
                  layoutPropertiesList.IsPersistent = true;

                  // Работаем со свойствами элемента
                  IProperty robDataProperty = layoutPropertiesList.Properties.FirstOrDefault(p => p.Name == "RobotWpsdfData");

                  if (robDataProperty != null)
                  {
                    robDataProperty.Value = serializedData;
                    ms.AppendMessage($"Updated property 'RobotWpsdfData' for component '{comp.Name}'", MessageLevel.Warning);
                  }
                  else
                  {
                    IProperty newRobDataProperty = layoutPropertiesList.CreateProperty(
                        serializedData.GetType(),
                        PropertyConstraintType.AllValuesAllowed,
                        "RobotWpsdfData"
                    );
                    newRobDataProperty.Value = serializedData;
                    newRobDataProperty.IsPersistent = true;
                    ms.AppendMessage($"Created new property 'RobotWpsdfData' for component '{comp.Name}'", MessageLevel.Warning);
                  }
                }

                robSettings[comp.Name] = $"WpsFilePath: {wpsFilePath}";
              }
              else
              {
                ms.AppendMessage($"WpsFilePath '{wpsFilePath}' does not exist for component '{comp.Name}'.", MessageLevel.Warning);
              }
            }
            catch (Exception ex)
            {
              ms.AppendMessage($"Error processing component '{comp.Name}': {ex.Message}", MessageLevel.Warning);
            }
          }
          else
          {
            ms.AppendMessage($"Component '{comp.Name}' does not have a 'RobotSettings' property.", MessageLevel.Warning);
          }
        }
      }

      // Вывод итоговой информации
      if (robSettings.Count > 0)
      {
        ms.AppendMessage($"Layout contains {robSettings.Count} robot(s)!", MessageLevel.Warning);
      }
      else
      {
        ms.AppendMessage("Layout contains 0 robot!", MessageLevel.Warning);
      }
    }
  }
}
