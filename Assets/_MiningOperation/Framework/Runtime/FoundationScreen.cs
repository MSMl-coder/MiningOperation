using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningOperation.Framework
{
    // Foundation v0.3: UI Toolkit only. No scene regeneration is required.
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class FoundationScreen : MonoBehaviour
    {
        public enum ScreenKind { MainMenu, EmptyGame }
        [SerializeField] private ScreenKind screen = ScreenKind.MainMenu;

        // Only used by the safe, repeatable Unity Editor scene setup command.
        public void Configure(ScreenKind value) => screen = value;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _column;
        private Label _status;
        private bool _busy;
        private int _uiGeneration;
        private string _page = "home";
        private FoundationSettingsData _settingsDraft;
        private string _settingsReturnPage = "home";

        private readonly Color _light = new Color(0.90f, 0.96f, 0.98f);
        private readonly Color _muted = new Color(0.59f, 0.72f, 0.76f);

        private bool _renderDeferred;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            // The Editor may enable this component while the setup script is still
            // assigning UIDocument.panelSettings. Do not render an incomplete document.
            if (!Application.isPlaying) return;
            if (_document == null || _document.panelSettings == null)
            {
                _renderDeferred = true;
                return;
            }
            _renderDeferred = false;
            Render();
        }

        private void Start()
        {
            // One safe retry after all scene components have been initialized.
            if (!_renderDeferred) return;
            _renderDeferred = false;
            _document = GetComponent<UIDocument>();
            if (_document == null || _document.panelSettings == null)
            {
                Debug.LogError("[MO/UI] Missing UIDocument or PanelSettings on " + name +
                    ". Exit Play Mode and run Mining Operation > Framework > 01 - Configure Foundation Scenes.", this);
                return;
            }
            Render();
        }

        private void OnDisable()
        {
            _renderDeferred = false;
            _uiGeneration++;
            if (_root != null) _root.Clear();
            _root = null;
            _column = null;
            _status = null;
        }

        private void Render()
        {
            FoundationInputService.Instance?.SetContext(_page == "settings" ? FoundationInputContext.Modal :
                screen == ScreenKind.EmptyGame ? FoundationInputContext.Gameplay : FoundationInputContext.Menu);
            if (_document == null || _document.panelSettings == null)
            {
                Debug.LogError("[MO/UI] UIDocument or PanelSettings missing on " + name);
                return;
            }
            _uiGeneration++;
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1;
            _root.style.width = Length.Percent(100);
            _root.style.height = Length.Percent(100);
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.055f, 0.085f, 0.11f);

            _column = new VisualElement();
            _column.style.width = 500;
            _column.style.maxWidth = Length.Percent(92);
            _column.style.paddingLeft = 15;
            _column.style.paddingRight = 15;
            if (_page == "settings")
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.width = Length.Percent(100);
                scroll.style.maxHeight = Length.Percent(100);
                scroll.style.alignItems = Align.Center;
                scroll.Add(_column);
                _root.Add(scroll);
            }
            else _root.Add(_column);

            Label("MINING OPERATION", 34, _light, 8);
            Label("FOUNDATION / MIGRATION 0.1", 12, _muted, 20);

            if (_page == "settings") RenderSettings();
            else if (screen == ScreenKind.EmptyGame) RenderGame();
            else if (_page == "new") RenderNewGame();
            else if (_page == "load") RenderLoadGame();
            else RenderMainMenu();

            _status = Label("", 12, _muted, 0);
        }

        private void RenderMainMenu()
        {
            Button("NEW GAME", () => { _page = "new"; Render(); });
            Button("LOAD GAME", () => { _page = "load"; Render(); LoadSlots(); });
            Button("SETTINGS", OpenSettings);
            Button("EXIT", () => FoundationBootstrap.Host?.Quit());
            Label("Saves are stored in Application.persistentDataPath", 11, _muted, 4);
        }

        private void RenderNewGame()
        {
            Label("CREATE SESSION", 17, _light, 12);
            var nameField = new TextField(FoundationLocalization.Translate("Session name")) { value = "New Session" };
            nameField.style.marginBottom = 12;
            _column.Add(nameField);
            Button("CREATE AND ENTER", () => RunAsync(async () =>
            {
                FoundationGameServices.Sessions.NewSession(nameField.value);
                await FoundationBootstrap.Host.OpenEmptyGameAsync();
            }));
            Button("BACK", () => { _page = "home"; Render(); });
        }

        private void RenderLoadGame()
        {
            Label("LOAD SESSION", 17, _light, 12);
            Label("Reading slots...", 13, _muted, 10);
            Button("BACK", () => { _page = "home"; Render(); });
        }

        private void LoadSlots()
        {
            int generation = _uiGeneration;
            RunAsync(async () =>
            {
                List<FoundationSaveSummary> slots = await FoundationGameServices.Sessions.GetSlotsAsync();
                if (generation != _uiGeneration || _root == null) return;
                // Build again so stale listings and buttons are completely removed.
                Render();
                _column.Clear();
                Label("LOAD SESSION", 18, _light, 16);
                if (slots.Count == 0) Label("No saves found", 14, _muted, 15);
                foreach (var slot in slots)
                {
                    var copy = slot;
                    if (slot.Valid)
                    {
                        Button(slot.DisplayName + "   |   " + slot.SavedUtc, () => RunAsync(async () =>
                        {
                            await FoundationGameServices.Sessions.LoadAsync(copy.SaveId);
                            await FoundationBootstrap.Host.OpenEmptyGameAsync();
                        }));
                    }
                    else Label("INVALID: " + slot.SaveId + " — " + slot.Error, 11, new Color(1f, 0.62f, 0.52f), 8);
                    Button("DELETE  " + slot.SaveId.Substring(0, 8), () => RunAsync(async () =>
                    {
                        await FoundationGameServices.Sessions.DeleteAsync(copy.SaveId);
                        _page = "load";
                        Render();
                        LoadSlots();
                    }));
                }
                Button("BACK", () => { _page = "home"; Render(); });
                _status = Label("", 12, _muted, 0);
            });
        }

        private void RenderGame()
        {
            var session = FoundationGameServices.Sessions.Active;
            if (session == null)
            {
                Label("No active session. Return to menu and select NEW GAME or LOAD GAME.", 14, _muted, 16);
                Button("RETURN TO MENU", () => RunAsync(() => FoundationBootstrap.Host.OpenMenuAsync()));
                return;
            }
            Label((FoundationLocalization.Language == "uk" ? "СЕСІЯ: " : "SESSION: ") + session.DisplayName, 18, _light, 12);
            Label((FoundationLocalization.Language == "uk" ? "ID СЕСІЇ: " : "SESSION ID: ") + session.SessionId, 11, _muted, 12);
            Label("FOUNDATION READY", 19, _light, 18);
            Button("SETTINGS", OpenSettings);
            Button("SAVE SESSION", () => RunAsync(async () =>
            {
                string id = await FoundationGameServices.Sessions.SaveAsync();
                if (_status != null) _status.text = "Saved: " + id.Substring(0, 8);
                Debug.Log("[MO/SAVE] Saved " + id);
            }));
            Button("SAVE & RETURN TO MENU", () => RunAsync(async () =>
            {
                await FoundationGameServices.Sessions.SaveAsync();
                FoundationGameServices.Sessions.CloseSession();
                await FoundationBootstrap.Host.OpenMenuAsync();
            }));
            Button("RETURN WITHOUT SAVING", () => RunAsync(async () =>
            {
                FoundationGameServices.Sessions.CloseSession();
                await FoundationBootstrap.Host.OpenMenuAsync();
            }));
        }

        private void OpenSettings()
        {
            _settingsReturnPage = _page;
            _page = "settings";
            _settingsDraft = null;
            Render();
            int generation = _uiGeneration;
            RunAsync(async () =>
            {
                await FoundationSettingsServices.EnsureLoadedAsync();
                if (this == null || _root == null || generation != _uiGeneration) return;
                _settingsDraft = FoundationSettingsServices.Settings.CreateDraft();
                Render();
            });
        }

        private void ExitSettings()
        {
            _settingsDraft = null;
            _page = _settingsReturnPage;
            Render();
            if (_page == "load") LoadSlots();
        }

        private void RenderSettings()
        {
            Label("SETTINGS", 20, _light, 12);
            if (_settingsDraft == null)
            {
                Label("Loading preferences...", 13, _muted, 14);
                return;
            }
            var d = _settingsDraft;
            Label("GRAPHICS", 15, _light, 8);
            string[] names = QualitySettings.names;
            if (names.Length > 0)
            {
                var quality = new DropdownField(FoundationLocalization.Translate("Quality preset"), new List<string>(names),
                    Mathf.Clamp(d.qualityLevel, 0, names.Length - 1));
                quality.RegisterValueChangedCallback(e =>
                    d.qualityLevel = Array.IndexOf(names, e.newValue));
                AddSettingField(quality);
            }
            var vsync = new Toggle(FoundationLocalization.Translate("VSync")) { value = d.vsync };
            vsync.RegisterValueChangedCallback(e => d.vsync = e.newValue);
            AddSettingField(vsync);
            var fpsOptions = new List<string> { "30", "60", "90", "120", "144", "240", "Unlimited" };
            string initial = d.fpsLimit < 0 ? "Unlimited" : d.fpsLimit.ToString();
            if (!fpsOptions.Contains(initial)) fpsOptions.Add(initial);
            var fps = new DropdownField(FoundationLocalization.Translate("FPS limit (when VSync is off)"), fpsOptions, initial);
            fps.RegisterValueChangedCallback(e => d.fpsLimit = e.newValue == "Unlimited" ? -1 : int.Parse(e.newValue));
            AddSettingField(fps);

            Label("AUDIO", 15, _light, 8);
            VolumeSlider("Master", d.masterVolume, v => d.masterVolume = v);
            VolumeSlider("Music", d.musicVolume, v => d.musicVolume = v);
            VolumeSlider("SFX", d.sfxVolume, v => d.sfxVolume = v);
            VolumeSlider("UI", d.uiVolume, v => d.uiVolume = v);
            VolumeSlider("Ambient", d.ambientVolume, v => d.ambientVolume = v);
            VolumeSlider("Voice", d.voiceVolume, v => d.voiceVolume = v);
            Button("TEST AUDIO (UI)", () => FoundationAudioService.Instance?.PlayTestClick());
            Label("Audio categories are applied to sounds played through FoundationAudioService.", 11, _muted, 8);

            Label("CONTROLS", 15, _light, 8);
            Label("Test action key: " + (FoundationInputService.Instance?.BindingDisplay ?? "unavailable"), 12, _muted, 8);
            Button("REBIND TEST ACTION", () => RunAsync(async () =>
            {
                if (FoundationInputService.Instance == null) throw new InvalidOperationException("Input service missing");
                if (_status != null) _status.text = FoundationLocalization.Translate("Press a key (Esc cancels)...");
                await FoundationInputService.Instance.StartRebindAsync();
                if (this != null) Render();
            }));
            Button("RESET TEST BINDING", () =>
            {
                FoundationInputService.Instance?.RestoreDefaultBinding();
                Render();
            });
            Label("Press assigned key to test input while playing.", 11, _muted, 8);

            Label("GENERAL", 15, _light, 8);
            var language = new DropdownField(FoundationLocalization.Translate("Language preference"), new List<string> { "English", "Українська" },
                d.language == "uk" ? "Українська" : "English");
            language.RegisterValueChangedCallback(e => d.language = e.newValue == "Українська" ? "uk" : "en");
            AddSettingField(language);
            var autosave = new Toggle(FoundationLocalization.Translate("Autosave enabled*")) { value = d.autosaveEnabled };
            autosave.RegisterValueChangedCallback(e => d.autosaveEnabled = e.newValue);
            AddSettingField(autosave);
            var interval = new IntegerField(FoundationLocalization.Translate("Autosave interval, minutes*")) { value = d.autosaveMinutes };
            interval.RegisterValueChangedCallback(e => d.autosaveMinutes = Mathf.Clamp(e.newValue, 1, 120));
            AddSettingField(interval);
            var notification = new Toggle(FoundationLocalization.Translate("Notifications*")) { value = d.notificationsEnabled };
            notification.RegisterValueChangedCallback(e => d.notificationsEnabled = e.newValue);
            AddSettingField(notification);
            Label("* Persisted preferences; gameplay integration is scheduled for later modules.", 11, _muted, 10);

            Button("APPLY AND SAVE", () => RunAsync(async () =>
            {
                await FoundationSettingsServices.Settings.ApplyAsync(d);
                FoundationAudioService.Instance?.ApplyPreferences();
                if (this != null) ExitSettings();
            }));
            Button("RESTORE DEFAULTS", () =>
            {
                _settingsDraft = FoundationSettingsService.Defaults();
                Render();
            });
            Button("CANCEL", ExitSettings);
            Label("Preferences file: MiningOperation/Settings/settings.json", 11, _muted, 5);
        }

        private void AddSettingField(VisualElement field)
        {
            field.style.marginBottom = 10;
            field.style.color = _light;
            _column.Add(field);
        }

        private void VolumeSlider(string caption, float value, Action<float> update)
        {
            var slider = new Slider(FoundationLocalization.Translate(caption.Replace("*", "")), 0f, 1f) { value = value };
            slider.RegisterValueChangedCallback(e => update(e.newValue));
            AddSettingField(slider);
        }

        private async void RunAsync(Func<Task> operation)
        {
            if (_busy) return;
            _busy = true;
            try { await operation(); }
            catch (Exception e) { ShowError(e); }
            finally { _busy = false; }
        }

        private void ShowError(Exception ex)
        {
            Debug.LogError("[DW] " + ex);
            if (_status != null) _status.text = "ERROR: " + ex.Message;
        }

        private Label Label(string value, int size, Color color, int marginBottom)
        {
            var label = new Label(FoundationLocalization.Translate(value));
            label.style.color = color;
            label.style.fontSize = size;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = marginBottom;
            _column.Add(label);
            return label;
        }

        private void Button(string caption, Action action)
        {
            var button = new Button(() =>
            {
                FoundationAudioService.Instance?.PlayTestClick();
                action?.Invoke();
            }) { text = FoundationLocalization.Translate(caption) };
            button.style.height = 48;
            button.style.marginBottom = 10;
            button.style.fontSize = 14;
            button.style.color = _light;
            button.style.backgroundColor = new Color(0.12f, 0.21f, 0.27f);
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            var border = new Color(0.30f, 0.48f, 0.54f);
            button.style.borderTopColor = border;
            button.style.borderBottomColor = border;
            button.style.borderLeftColor = border;
            button.style.borderRightColor = border;
            _column.Add(button);
        }
    }
}
