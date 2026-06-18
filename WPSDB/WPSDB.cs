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

      UpdateRobWpsdfDataInLayout(robotsData, world);

    }

    /// <summary>
    /// Получаем данные WPSDF для всех роботов из World и возвращаем их в виде словаря.
    /// </summary>
    /// <param name="app">Объект приложения IApplication.</param>
    /// <param name="world">Объект мира ISimWorld, содержащий компоненты роботов.</param>
    /// <returns>
    /// Возвращает словарь, где ключом является имя компонента робота, а значением -
    /// объект RobotData, содержащий данные WPSDF и другую информацию для данного робота.
    /// </returns>
    private Dictionary<string, RobotData> GetRobotsData(IApplication app, ISimWorld world)
    {  
      Dictionary<string, RobotData> robotData = new Dictionary<string, RobotData>();
      foreach (var comp in world.Components)
      {
        if (comp.Behaviors.Any(beh => beh.Type == BehaviorType.RobotController))
        {
          string robName = comp.Name.Replace(' ', '_');
          string wpsFilePath = string.Empty;
          string wpsFileBuffer = DEFAULT_WPSDF_PATH + robName + WPSPATH_END;
          bool isSameFile = false;
          string serializedData = "{}";
          IProperty robSettingsProp = comp.Properties.FirstOrDefault(p => p.Name == "RobotSettings");
          string settingsValue = robSettingsProp.Value?.ToString() ?? "{}";
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
            else
            {
              IMessageBoxService mbs = IoC.Get<IMessageBoxService>();
              var result = mbs.Show($"WpsFilePath '{wpsFilePath}' does not exist for component '{comp.Name}'. A default path will be used: {wpsFileBuffer}","WPS File path check");
            }
          }
          catch (Exception ex)
          {
            IMessageService ms = IoC.Get<IMessageService>();
            ms.AppendMessage($"Error processing component '{comp.Name}': {ex.Message}", MessageLevel.Warning);
          }
          robotData[comp.Name] = new RobotData
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
      return robotData;
    }
    /// <summary>
    /// Проверяет существование буферного файла WPSDF для каждого робота и создаёт его, если он не существует.
    /// </summary>
    /// <param name="robotsData">Словарь, где ключом является имя компонента робота, а значением -
    /// объект RobotData, содержащий данные WPSDF и другую информацию для данного робота.</param>
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
    private void UpdateRobBuffFile(Dictionary<string, RobotData> robotsData, ISimWorld world)
    {

    }

    /// <summary>
    /// Получает данные WPSDF для конкретного робота из layout item, если он существует, или возвращает null, если элемента нет.
    /// </summary>
    /// <param name="robName"></param>
    /// <returns></returns>
    private string GetRobWpsdfDataFromLY(String robName)
    {
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

    /// <summary>
    /// Обновляет данные WPSDF для каждого робота в layout, создавая или обновляя соответствующий 
    /// элемент и его свойство "RobotWpsdfData" с сериализованными данными WPSDF из robotsData.
    /// </summary>
    /// <param name="robotsData">Словарь с данными о всех роботах </param>
    /// <param name="world">Текущий 3D Мир</param>
    private void UpdateRobWpsdfDataInLayout(Dictionary<string, RobotData> robotsData, ISimWorld world)
    {
      IMessageService ms = IoC.Get<IMessageService>();
      //Проверяем существование элемента в layout с именем wpsItemName
      foreach (var robotData in robotsData.Values)
      {
        ILayoutPropertyList layoutPropertiesList = null;
        // Проверяем, существует ли уже элемент с таким именем
        var isItemexis = world.LayoutItems.FirstOrDefault(li => li.Name == robotData.WpsItemName);

        if (isItemexis != null)
        {
          // Если существует, используем его
          layoutPropertiesList = isItemexis as ILayoutPropertyList;
          if (layoutPropertiesList == null)
          {
            // Если элемент существует, но не того типа, удаляем и создаём заново
            world.DeleteLayoutItem(isItemexis);
            layoutPropertiesList = world.CreateLayoutItem<ILayoutPropertyList>(robotData.WpsItemName);
          }
          else
          {
            ms.AppendMessage($"Using existing layout item: '{robotData.WpsItemName}'", MessageLevel.Info);
          }
        }
        else
        {
          // Если не существует, создаём новый
          layoutPropertiesList = world.CreateLayoutItem<ILayoutPropertyList>(robotData.WpsItemName);
          ms.AppendMessage($"Created new layout item: '{robotData.WpsItemName}'", MessageLevel.Info);
        }

        if (layoutPropertiesList != null)
        {
          layoutPropertiesList.IsPersistent = true;

          // Работаем со свойствами элемента
          IProperty robDataProperty = layoutPropertiesList.Properties.FirstOrDefault(p => p.Name == "RobotWpsdfData");

          if (robDataProperty != null)
          {
            robDataProperty.Value = robotData.SerializedData;
            ms.AppendMessage($"Updated property 'RobotWpsdfData' for component '{robotData.RobotComponent}'", MessageLevel.Info);
          }
          else
          {
            IProperty newRobDataProperty = layoutPropertiesList.CreateProperty(
                robotData.SerializedData.GetType(),
                PropertyConstraintType.AllValuesAllowed,
                "RobotWpsdfData"
            );
            newRobDataProperty.Value = robotData.SerializedData;
            newRobDataProperty.IsPersistent = true;
            ms.AppendMessage($"Created new property 'RobotWpsdfData' for component '{robotData.RobotComponent}'", MessageLevel.Info);
          }
        }
      }
    }
  }
}
/// <summary>
/// Данный класс RobotData используется для хранения информации о каждом роботе, включая ссылку 
/// на его компонент в World, путь к файлу WPSDF, флаг совпадения пути к буферному файлу
/// </summary>
public class RobotData
{
  // Ссылка на компонент робота в World, для которого хранятся данные WPSDF
  public ISimComponent RobotComponent { get; set; }
  // Путь к файлу WPSDF для данного робота, полученный из его свойств RobotSettings
  public string WpsFilePath { get; set; }
  // Флаг, указывающий, совпадает ли путь к буферному файлу WPSDF с текущим путем к файлу WPSDF для данного робота
  public bool IsSameFile { get; set; }
  // Путь к буферному файлу WPSDF для данного робота (например, C:\Users\Public\Documents\Delfoi\WPS\WPS_DataFile_Robot1.wpsdf)
  public string WpsFileBuffer { get; set; }
  // Флаг, указывающий, существует ли буферный файл WPSDF для данного робота
  public bool WpsFileExist { get; set; }
  // Сериализованные данные WPSDF для хранения в layout item
  public string SerializedData { get; set; } = "{}";
  //Имя элемента в layout для хранения данных WPSDF, например "KWS_Robot1"
  public string WpsItemName { get; set; }
}
