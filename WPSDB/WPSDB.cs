using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;


using Newtonsoft.Json.Linq;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using VisualComponents.Create3D;
using VisualComponents.UX.Shared;
using Newtonsoft.Json;

namespace WPSDB
{
  [Export(typeof(IPlugin))]
  public class WPSDB : IPlugin
  {
    const string LAYOUT_ITEM_NAME = "KWS_";
    const string DEFAULT_WPSDF_PATH = @"C:\Users\Public\Documents\Delfoi\WPS\WPS_DataFile_";
    const string WPSPATH_END = @".wpsdf";
    //Dictionary<ISimComponent, bool> buffFileExist = new Dictionary<ISimComponent, bool>();
    ////bool buffFileExist = false;
    bool sameFile = false;
    string serializedData = "{}";
    Dictionary<ISimComponent, string> robSettings = new Dictionary<ISimComponent, string>();
    Dictionary<string, RobotData> robotsData = new Dictionary<string, RobotData>();

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
      IApplication app = _app.Value;
      ISimWorld world = app.World;
      bool hasRobots = LayoutHasRobots(world);
      IMessageService ms = IoC.Get<IMessageService>();
      //Dictionary<ISimComponent, string> robotData = GetWPSRobotDataFromLayout(app, world);
    }

    private void AppLayoutLoading(object sender, LayoutLoadingEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      ms.AppendMessage("Layout Load Started", MessageLevel.Warning);
    }

    private void AppLayoutSaved(object sender, LayoutSavedEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      // Если буферный файл существует и не совпадает с текущим файлом, сохраняем данные в буферный файл
      foreach (var robotData in robotsData.Values) 
      
      { 
        if (robotData.WpsFileExist && !robotData.IsSameFile)
        { 
          File.WriteAllText(robotData.WpsFileBuffer, robotData.SerializedData);
        }
        IProperty ropSettingsProp = robotData.RobotComponent.Properties.FirstOrDefault(p => p.Name == "RobotSettings");
        string settingsValue = ropSettingsProp.Value?.ToString() ?? "{}";
        if (!string.IsNullOrEmpty(settingsValue) && settingsValue != "{}")
        {
          try
          {
            var jsonObject = JObject.Parse(settingsValue);
            jsonObject["WpsFilePath"] = robotData.WpsFileBuffer;
            ropSettingsProp.Value = jsonObject.ToString(Formatting.None);
          }
          catch (Exception ex)
          {
            ms.AppendMessage($"Error updating WpsFilePath for component '{robotData.RobotComponent.Name}': {ex.Message}", MessageLevel.Error);
          }
        }
      }
    ms.AppendMessage("Layout Saving Finished", MessageLevel.Warning);
    }

    private bool LayoutHasRobots(ISimWorld world)
    {
      return world.Components.Any(comp => comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController));
    }

    private void AppLayoutSaving(object sender, LayoutSavingEventArgs e)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      IApplication app = _app.Value;
      ISimWorld world = app.World;

      robotsData = GetRobotsData(app, world);

      CheckRobWPSBuffer(robotsData);


      //foreach (var comp in world.Components)
      //{
      //  if (comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController))
      //  {
      //    IProperty ropSettingsProp = comp.Properties.FirstOrDefault(p => p.Name == "RobotSettings");
      //    if (ropSettingsProp != null)
      //    {
      //      string robName = comp.Name;
      //      string wpsItemName = LAYOUT_ITEM_NAME + robName;
      //      string settingsValue = ropSettingsProp.Value?.ToString() ?? "{}";

      //      try
      //      {
      //        // Парсим JSON-строку в JObject
      //        JObject jWpsFilePath = JObject.Parse(settingsValue);
      //        // Получаем значение WpsFilePath
      //        string wpsFilePath = jWpsFilePath["WpsFilePath"]?.ToString() ?? "No WpsFilePath";

      //        ms.AppendMessage($"Processing component '{comp.Name}' with WpsFilePath: {wpsFilePath}", MessageLevel.Warning);

      //        this.sameFile = string.Equals(wpsFilePath, DEFAULT_WPSDF_PATH, StringComparison.OrdinalIgnoreCase);

      //        // Проверяем существование файла
      //        if (!string.IsNullOrEmpty(wpsFilePath) && File.Exists(wpsFilePath))
      //        {
      //          // Читаем содержимое файла WPSDF
      //          string wpsdfContent = File.ReadAllText(wpsFilePath);
      //          // Парсим содержимое WPSDF в JObject
      //          JObject wpsdfObject = JObject.Parse(wpsdfContent);
      //          // Сериализуем объект обратно в строку
      //          this.serializedData = wpsdfObject.ToString();

      //          // Проверяем существование элемента в layout с именем wpsItemName
      //          ILayoutPropertyList layoutPropertiesList = null;

      //          // Проверяем, существует ли уже элемент с таким именем
      //          var existingItem = world.LayoutItems.FirstOrDefault(li => li.Name == wpsItemName);

      //          if (existingItem != null)
      //          {
      //            // Если существует, используем его
      //            layoutPropertiesList = existingItem as ILayoutPropertyList;
      //            if (layoutPropertiesList == null)
      //            {
      //              // Если элемент существует, но не того типа, удаляем и создаём заново
      //              world.DeleteLayoutItem(existingItem);
      //              layoutPropertiesList = world.CreateLayoutItem<ILayoutPropertyList>(wpsItemName);
      //            }
      //            else
      //            {
      //              ms.AppendMessage($"Using existing layout item: '{wpsItemName}'", MessageLevel.Warning);
      //            }
      //          }
      //          else
      //          {
      //            // Если не существует, создаём новый
      //            layoutPropertiesList = world.CreateLayoutItem<ILayoutPropertyList>(wpsItemName);
      //            ms.AppendMessage($"Created new layout item: '{wpsItemName}'", MessageLevel.Warning);
      //          }

      //          if (layoutPropertiesList != null)
      //          {
      //            layoutPropertiesList.IsPersistent = true;

      //            // Работаем со свойствами элемента
      //            IProperty robDataProperty = layoutPropertiesList.Properties.FirstOrDefault(p => p.Name == "RobotWpsdfData");

      //            if (robDataProperty != null)
      //            {
      //              robDataProperty.Value = this.serializedData;
      //              ms.AppendMessage($"Updated property 'RobotWpsdfData' for component '{comp.Name}'", MessageLevel.Warning);
      //            }
      //            else
      //            {
      //              IProperty newRobDataProperty = layoutPropertiesList.CreateProperty(
      //                  this.serializedData.GetType(),
      //                  PropertyConstraintType.AllValuesAllowed,
      //                  "RobotWpsdfData"
      //              );
      //              newRobDataProperty.Value = serializedData;
      //              newRobDataProperty.IsPersistent = true;
      //              ms.AppendMessage($"Created new property 'RobotWpsdfData' for component '{comp.Name}'", MessageLevel.Warning);
      //            }
      //          }

      //          robSettings[comp] = $"WpsFilePath: {wpsFilePath}";
      //        }
      //        else
      //        {
      //          ms.AppendMessage($"WpsFilePath '{wpsFilePath}' does not exist for component '{comp.Name}'.", MessageLevel.Warning);
      //        }
      //      }
      //      catch (Exception ex)
      //      {
      //        ms.AppendMessage($"Error processing component '{comp.Name}': {ex.Message}", MessageLevel.Warning);
      //      }
      //    }
      //    else
      //    {
      //      ms.AppendMessage($"Component '{comp.Name}' does not have a 'RobotSettings' property.", MessageLevel.Warning);
      //    }
      //  }
      //}
    }

    private Dictionary<string, RobotData> GetRobotsData(IApplication app, ISimWorld world)
    {
      // Получаем данные WPSDF для всех роботов из World и возвращаем их в виде словаря
      Dictionary<string, RobotData> robotData = new Dictionary<string, RobotData>();
      foreach (var comp in world.Components)
      {
        if (comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController))
        {
          string robName = comp.Name.Replace(' ', '_');
          string wpsFilePath = string.Empty;
          string wpsFileBuffer = DEFAULT_WPSDF_PATH + robName + WPSPATH_END;
          bool isSameFile = false;
          serializedData = null;
          IProperty ropSettingsProp = comp.Properties.FirstOrDefault(p => p.Name == "RobotSettings");
          string settingsValue = ropSettingsProp.Value?.ToString() ?? "{}";
          string wpsItemName = LAYOUT_ITEM_NAME + robName;
          try
          {
            // Парсим JSON-строку в JObject
            JObject jWpsFilePath = JObject.Parse(settingsValue);
            // Получаем значение WpsFilePath
            wpsFilePath = jWpsFilePath["WpsFilePath"]?.ToString() ?? "No WpsFilePath";
            isSameFile = string.Equals(wpsFilePath, wpsFileBuffer, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(wpsFilePath) && File.Exists(wpsFilePath))
            {
              // Читаем содержимое файла WPSDF текущего робота
              string wpsdfContent = File.ReadAllText(wpsFilePath);
              // Парсим содержимое WPSDF в JObject
              JObject wpsdfObject = JObject.Parse(wpsdfContent);
              // Сериализуем объект обратно в строку для хранения в классе RobotData
              serializedData = wpsdfObject.ToString();
            }
          }
          catch (Exception ex)
          {
            IMessageService ms = IoC.Get<IMessageService>();
            ms.AppendMessage($"Error processing component '{comp.Name}': {ex.Message}", MessageLevel.Warning);
          }
          
          if (!string.IsNullOrEmpty(serializedData))
          {
            robotData[robName] = new RobotData
            {
              RobotComponent = comp,
              WpsFilePath = wpsFilePath,
              IsSameFile = isSameFile,
              WpsFileBuffer = wpsFileBuffer,
              SerializedData = serializedData,
              WpsItemName = wpsItemName
            };
          }
        }
      }
      return robotData;
    }

    private void CheckRobWPSBuffer (Dictionary<string, RobotData> robotsData)
    {
      IMessageService ms = IoC.Get<IMessageService>();

      if (robotsData.Count > 0)
      {
        foreach (var robotData in robotsData.Values)
        {

          robotData.WpsFileExist = File.Exists(robotData.WpsFileBuffer);
          if (robotData.WpsFileExist)
          {
            ms.AppendMessage($"Default WPSDF file found at: {robotData.WpsFileBuffer}", MessageLevel.Warning);
          }
          else
          {
            try
            {
              Directory.CreateDirectory(Path.GetDirectoryName(robotData.WpsFileBuffer)); // Создаём директорию, если её нет
              File.Create(robotData.WpsFileBuffer).Close(); // Создаём пустой файл, если его нет
              robotData.WpsFileExist = true;
              ms.AppendMessage($"Default WPSDF was created to path: {robotData.WpsFileBuffer}", MessageLevel.Warning);
            }
            catch (Exception ex)
            {
              ms.AppendMessage($"Error creating directory for default WPSDF of robot {robotData.RobotComponent}: {ex.Message}", MessageLevel.Error);
              robotData.WpsFileExist = false;
            }
          }

        }
      }
    }
    private Dictionary<ISimComponent, string> GetWPSRobotDataFromLayout(IApplication app, ISimWorld world)
    {
      // Получаем данные WPSDF для всех роботов в layout и возвращаем их в виде словаря
      Dictionary<ISimComponent, string> robotData = new Dictionary<ISimComponent, string>();
      foreach (var comp in world.Components)
      {
        if (comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController))
        {
          string robName = comp.Name;
          string wpsdfData = GetRobWpsdfDataFromLY(robName);
          if (!string.IsNullOrEmpty(wpsdfData))
          {
            robotData[comp] = wpsdfData;
          }
        }
      }
      return robotData;
    }

    private string GetRobWpsdfDataFromLY(String robName)
    {
      
      // Возвращаем данные WPSDF из layout item в зависимости от имени робота или null
      ILayoutPropertyList layoutPropertiesList = null;
      IApplication app = _app.Value;
      ISimWorld world = app.World;

      string wpsItemName = LAYOUT_ITEM_NAME + robName;
      var existingItem = world.LayoutItems.FirstOrDefault(li => li.Name == wpsItemName);
          
      if (existingItem != null)
      {
        layoutPropertiesList = existingItem as ILayoutPropertyList;
        IProperty robDataProperty = layoutPropertiesList.Properties.FirstOrDefault(p => p.Name == "RobotWpsdfData");
        return robDataProperty.Value.ToString();
      }
        return null;
      }
  }
}

public class RobotData
{
   public ISimComponent RobotComponent { get; set; }
   public string WpsFilePath { get; set; }
   public bool IsSameFile { get; set; }
   public string WpsFileBuffer { get; set; }
   public bool WpsFileExist { get; set; }
   public string SerializedData { get; set; } = "{}";
  //Имя элемента в layout для хранения данных WPSDF, например "KWS_Robot1"
  public string WpsItemName { get; set; }
}
