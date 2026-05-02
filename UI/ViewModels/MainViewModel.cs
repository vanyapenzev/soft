using VagImmoEditor.Pro.Data.Maps;
using VagImmoEditor.Pro.Data.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace VagImmoEditor.Pro.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly EepromDataService _eepromService;
        private readonly ImmoParserService _parserService;

        [ObservableProperty]
        private string _fileName = "No file loaded";

        [ObservableProperty]
        private string _pinCode = "N/A";

        [ObservableProperty]
        private int _mileage;

        [ObservableProperty]
        private string? _vin;

        [ObservableProperty]
        private string _immoType = "Unknown";

        [ObservableProperty]
        private string _manufacturer = "Unknown";

        [ObservableProperty]
        private string _crcStatus = "N/A";

        [ObservableProperty]
        private ushort _storedCrc;

        [ObservableProperty]
        private ushort _calculatedCrc;

        [ObservableProperty]
        private int _keyCount;

        [ObservableProperty]
        private bool _componentProtection;

        [ObservableProperty]
        private bool _learningMode;

        [ObservableProperty]
        private bool _immobilizerActive = true;

        [ObservableProperty]
        private short _countryCode;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _canUndo;

        [ObservableProperty]
        private bool _canRedo;

        [ObservableProperty]
        private EepromMap? _selectedMap;

        [ObservableProperty]
        private ObservableCollection<EepromMap> _availableMaps = new();

        public ObservableCollection<ImmoOption> Options { get; } = new();

        public MainViewModel()
        {
            _eepromService = new EepromDataService();
            _parserService = new ImmoParserService(_eepromService);
            
            LoadAvailableMaps();
            InitializeOptions();
        }

        private void LoadAvailableMaps()
        {
            AvailableMaps.Clear();
            foreach (var map in EepromMapService.GetAllMaps())
            {
                AvailableMaps.Add(map);
            }
            
            if (AvailableMaps.Count > 0)
                SelectedMap = AvailableMaps[0];
        }

        private void InitializeOptions()
        {
            Options.Add(new ImmoOption
            {
                Id = "component_protection",
                Name = "Component Protection",
                Description = "Защита компонентов - требует онлайн активации у дилера",
                IsEnabled = false,
                Tooltip = "Включите для активации защиты компонентов"
            });
            Options.Add(new ImmoOption
            {
                Id = "learning_mode",
                Name = "Learning Mode",
                Description = "Режим обучения ключей",
                IsEnabled = false,
                Tooltip = "Включите для добавления новых ключей"
            });
            Options.Add(new ImmoOption
            {
                Id = "immobilizer_active",
                Name = "Immobilizer Active",
                Description = "Активность иммобилайзера",
                IsEnabled = true,
                Tooltip = "Отключите для деактивации иммобилайзера"
            });
        }

        [RelayCommand]
        private void OpenFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Open EEPROM dump"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = _eepromService.LoadFromFile(dialog.FileName);
                if (result.Success)
                {
                    FileName = System.IO.Path.GetFileName(dialog.FileName);
                    ParseAndDisplayData();
                    StatusMessage = result.Message;
                }
                else
                {
                    StatusMessage = $"Error: {result.Message}";
                }
            }
        }

        [RelayCommand]
        private void SaveFile()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Save EEPROM dump",
                FileName = System.IO.Path.GetFileNameWithoutExtension(FileName) + "_modified.bin"
            };

            if (dialog.ShowDialog() == true)
            {
                // Обновляем данные из UI перед сохранением
                UpdateEepromFromUi();
                
                // Пересчитываем CRC если нужно
                RecalculateCrc();
                
                var result = _eepromService.SaveToFile(dialog.FileName);
                StatusMessage = result.Success ? result.Message : $"Error: {result.Message}";
            }
        }

        [RelayCommand]
        private void Undo()
        {
            var result = _eepromService.Undo();
            if (result.Success)
            {
                ParseAndDisplayData();
                StatusMessage = result.Message;
            }
            UpdateUndoRedoState();
        }

        [RelayCommand]
        private void Redo()
        {
            var result = _eepromService.Redo();
            if (result.Success)
            {
                ParseAndDisplayData();
                StatusMessage = result.Message;
            }
            UpdateUndoRedoState();
        }

        partial void OnSelectedMapChanged(EepromMap? value)
        {
            if (value != null && _eepromService.Data.Length > 0)
            {
                ParseAndDisplayData();
            }
        }

        private void ParseAndDisplayData()
        {
            var immoData = _parserService.ParseImmoData(SelectedMap);

            PinCode = immoData.PinCode;
            Mileage = immoData.Mileage;
            Vin = immoData.Vin;
            ImmoType = immoData.ImmoTypeName;
            Manufacturer = immoData.ManufacturerName;
            CrcStatus = immoData.CrcStatus;
            StoredCrc = immoData.StoredCrc;
            CalculatedCrc = immoData.CalculatedCrc;
            KeyCount = immoData.KeyCount;
            ComponentProtection = immoData.ComponentProtection;
            LearningMode = immoData.LearningMode;
            ImmobilizerActive = immoData.IsImmobilizerActive;
            CountryCode = immoData.CountryCode;

            // Обновляем опции
            if (Options.Count >= 3)
            {
                Options[0].IsEnabled = ComponentProtection;
                Options[1].IsEnabled = LearningMode;
                Options[2].IsEnabled = ImmobilizerActive;
            }

            UpdateUndoRedoState();
        }

        private void UpdateEepromFromUi()
        {
            var map = SelectedMap ?? EepromMapService.GetMapByImmoType(DetectImmoType());
            if (map == null) return;

            // Обновление PIN кода
            if (!string.IsNullOrEmpty(PinCode) && PinCode != "N/A" && PinCode != "Invalid")
            {
                try
                {
                    var pinBytes = Core.Codecs.BcdCodec.Encode(PinCode, map.PinLength * 2);
                    _eepromService.SetRange(map.PinOffset, pinBytes, "Update PIN");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Invalid PIN format: {ex.Message}";
                }
            }

            // Обновление пробега
            var mileageValue = Mileage / map.MileageMultiplier;
            if (map.MileageIsBcd)
            {
                var mileageBytes = Core.Codecs.BcdCodec.EncodeFromInt(mileageValue, map.MileageLength * 2);
                _eepromService.SetRange(map.MileageOffset, mileageBytes, "Update Mileage");
            }

            // Обновление флагов
            if (map.FlagsOffset >= 0)
            {
                byte flags = _eepromService.ReadByte(map.FlagsOffset);
                
                if (map.ComponentProtectionBit >= 0)
                {
                    if (ComponentProtection)
                        flags |= (byte)(1 << map.ComponentProtectionBit);
                    else
                        flags &= (byte)~(1 << map.ComponentProtectionBit);
                }
                
                if (map.LearningModeBit >= 0)
                {
                    if (LearningMode)
                        flags |= (byte)(1 << map.LearningModeBit);
                    else
                        flags &= (byte)~(1 << map.LearningModeBit);
                }
                
                if (map.ImmobilizerActiveBit >= 0)
                {
                    if (ImmobilizerActive)
                        flags |= (byte)(1 << map.ImmobilizerActiveBit);
                    else
                        flags &= (byte)~(1 << map.ImmobilizerActiveBit);
                }
                
                _eepromService.WriteByte(map.FlagsOffset, flags, "Update Flags");
            }

            // Обновление количества ключей
            if (map.KeyCountOffset >= 0)
            {
                _eepromService.WriteByte(map.KeyCountOffset, (byte)KeyCount, "Update Key Count");
            }
        }

        private void RecalculateCrc()
        {
            var map = SelectedMap ?? EepromMapService.GetMapByImmoType(DetectImmoType());
            if (map == null || map.CrcOffset < 0) return;

            _eepromService.UpdateCrc(map.CrcAlgorithm, map.CrcStartOffset, map.CrcLength, map.CrcOffset);
        }

        private ImmoType DetectImmoType()
        {
            return _parserService.DetectImmoType();
        }

        private void UpdateUndoRedoState()
        {
            CanUndo = _eepromService.CanUndo;
            CanRedo = _eepromService.CanRedo;
        }

        [RelayCommand]
        private void ExportToJson()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Export data to JSON"
            };

            if (dialog.ShowDialog() == true)
            {
                var immoData = _parserService.ParseImmoData(SelectedMap);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(immoData, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(dialog.FileName, json);
                StatusMessage = "Data exported to JSON";
            }
        }

        [RelayCommand]
        private void CompareFiles()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Select file to compare"
            };

            if (dialog.ShowDialog() == true)
            {
                var originalData = _eepromService.Data;
                var compareResult = _eepromService.LoadFromFile(dialog.FileName);
                
                if (compareResult.Success)
                {
                    var compareData = _eepromService.Data;
                    var differences = new List<string>();
                    
                    for (int i = 0; i < Math.Min(originalData.Length, compareData.Length); i++)
                    {
                        if (originalData[i] != compareData[i])
                        {
                            differences.Add($"Offset 0x{i:X4}: 0x{originalData[i]:X2} -> 0x{compareData[i]:X2}");
                        }
                    }
                    
                    // Восстанавливаем оригинальные данные
                    _eepromService.LoadFromBytes(originalData);
                    
                    if (differences.Count == 0)
                        StatusMessage = "Files are identical";
                    else
                        StatusMessage = $"Found {differences.Count} differences. Check log for details.";
                }
            }
        }
    }
}
