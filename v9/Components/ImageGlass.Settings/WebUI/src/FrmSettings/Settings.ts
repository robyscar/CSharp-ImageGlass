import TabGeneral from './TabGeneral';
import TabImage from './TabImage';
import TabSlideshow from './TabSlideshow';
import TabMouse from './TabMouse';
import TabKeyboard from './TabKeyboard';
import TabLanguage from './TabLanguage';
import TabEdit from './TabEdit';
import TabViewer from './TabViewer';
import TabToolbar from './TabToolbar';
import TabGallery from './TabGallery';
import TabFileAssocs from './TabFileAssocs';
import TabTools from './TabTools';
import TabAppearance from './TabAppearance';
import TabLayout from './TabLayout';

export default class Settings {
  /**
   * Loads settings.
   */
  static load() {
    Settings.loadSelectBoxEnums();
    TabLanguage.loadLanguageList();


    // auto loads settings for String, Number, Boolean
    for (const configKey in _pageSettings.config) {
      if (!Object.prototype.hasOwnProperty.call(_pageSettings.config, configKey)) {
        continue;
      }

      const configValue = _pageSettings.config[configKey];

      // only auto load the settings if the value type is supported
      const canAutoSet = typeof configValue === 'string'
        || typeof configValue === 'number'
        || typeof configValue === 'boolean';
      if (!canAutoSet) continue;


      // find the html element
      const el = query(`[name="${configKey}"]`, true);
      if (!el) {
        console.info(`>> Settings.load(): config '${configKey}' not found`);
        continue;
      }


      // check the tag name and type
      const tagName = el.tagName.toLowerCase();
      if (tagName === 'select') {
        (el as HTMLSelectElement).value = configValue.toString();
      }
      else if (tagName === 'input') {
        const inputType = el.getAttribute('type').toLowerCase();
        const inputEl = el as HTMLInputElement;

        if (inputType === 'radio' || inputType === 'checkbox') {
          inputEl.checked = Boolean(configValue);
        }
        else if (inputType === 'color') {
          const colorHex = configValue.toString() || '#00000000';

          // remove alpha
          inputEl.value = colorHex.substring(0, colorHex.length - 2);
        }
        else {
          inputEl.value = configValue.toString();
        }
      }
    }


    // load specific settings
    TabGeneral.loadSettings();
    TabImage.loadSettings();
    TabSlideshow.loadSettings();
    TabEdit.loadSettings();
    TabViewer.loadSettings();
    TabToolbar.loadSettings();
    TabGallery.loadSettings();
    TabLayout.loadSettings();
    TabMouse.loadSettings();
    TabKeyboard.loadSettings();
    TabFileAssocs.loadSettings();
    TabTools.loadSettings();
    TabLanguage.loadSettings();
    TabAppearance.loadSettings();

    // Adds event listeners for all settings.
    Settings.addEventListeners();
  }


  /**
   * Adds event listeners for all settings.
   */
  private static addEventListeners() {
    Settings.addEventsForFooter();
    TabGeneral.addEvents();
    TabImage.addEvents();
    TabSlideshow.addEvents();
    TabEdit.addEvents();
    TabViewer.addEvents();
    TabToolbar.addEvents();
    TabGallery.addEvents();
    TabLayout.addEvents();
    TabMouse.addEvents();
    TabFileAssocs.addEvents();
    TabTools.addEvents();
    TabLanguage.addEvents();
    TabAppearance.addEvents();
  }


  /**
   * Adds events for the footer of setting.
   */
  private static addEventsForFooter() {
    query('#BtnCancel').addEventListener('click', () => post('BtnCancel'), false);

    query('#BtnOK').addEventListener('click', () => {
      const allSettings = Settings.getAllSettings();
      Settings.updateInitSettings(allSettings);

      post('BtnOK', allSettings, true);
    }, false);

    query('#BtnApply').addEventListener('click', () => {
      const allSettings = Settings.getAllSettings();
      Settings.updateInitSettings(allSettings);

      post('BtnApply', allSettings, true);
    }, false);
  }


  /**
   * Gets all settings as an object.
   */
  static getAllSettings() {
    const settings: Record<string, any> = {
      ...TabGeneral.exportSettings(),
      ...TabImage.exportSettings(),
      ...TabSlideshow.exportSettings(),
      ...TabEdit.exportSettings(),
      ...TabViewer.exportSettings(),
      ...TabToolbar.exportSettings(),
      ...TabGallery.exportSettings(),
      ...TabLayout.exportSettings(),
      ...TabMouse.exportSettings(),
      ...TabKeyboard.exportSettings(),
      ...TabFileAssocs.exportSettings(),
      ...TabTools.exportSettings(),
      ...TabLanguage.exportSettings(),
      ...TabAppearance.exportSettings(),
    };

    return settings;
  }


  /**
   * Updates the `_pageSettings.config`.
   */
  static updateInitSettings(newSettings: Record<string, any>) {
    const settingKeys = Object.keys(newSettings);

    settingKeys.forEach(key => {
      if (_pageSettings.config.hasOwnProperty(key)) {
        _pageSettings.config[key] = newSettings[key];
      }
    });
  }


  /**
   * Loads select box items.
   */
  private static loadSelectBoxEnums() {
    // load enums
    for (const enumName in _pageSettings.enums) {
      if (!Object.prototype.hasOwnProperty.call(_pageSettings.enums, enumName)) {
        continue;
      }

      const enumKeys = _pageSettings.enums[enumName];
      const selectEls = queryAll<HTMLSelectElement>(`select[data-enum="${enumName}"]`, true);

      for (const el of selectEls) {
        enumKeys.forEach(key => {
          const optionEl = new Option(`${key}`, key);
          optionEl.setAttribute('lang-text', `_.${enumName}._${key}`);

          el.add(optionEl);
        });
      }
    }
  }

}
