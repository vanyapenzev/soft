# VAG IMMO Editor Pro

Профессиональный инструмент для работы с EEPROM иммобилайзеров VAG (IMMO2/3/4).

## Возможности

- ✅ Чтение и запись BIN файлов EEPROM (24LC64 - 8KB)
- ✅ Автоопределение типа иммобилайзера (VDO, Motorola, NEC, Kayaba)
- ✅ Парсинг PIN кода, пробега, VIN номера
- ✅ Проверка и пересчет CRC (CCITT и Motorola)
- ✅ Редактирование опций иммобилайзера
- ✅ Система Undo/Redo для всех изменений
- ✅ Поддержка BCD и ASCII кодирования
- ✅ Логирование всех операций

## Системные требования

- Windows 10/11
- .NET 8.0 SDK

## Сборка и запуск

```bash
dotnet restore
dotnet build
dotnet run
```

## Структура проекта

```
VagImmoEditor/
├── Core/           # Ядро (CRC, кодеки, криптография)
├── Data/           # Модели данных и карты памяти
├── Services/       # Сервисы работы с EEPROM и парсинга
├── UI/             # WPF интерфейс (ViewModels, Views)
└── Tools/          # Вспомогательные инструменты
```

## Поддерживаемые типы IMMO

| Тип | Производитель | Автомобили |
|-----|--------------|------------|
| IMMO2 |早期 системы | Golf III, Passat B4 (до 1998) |
| IMMO3 VDO | Siemens/VDO | A4/A6, Passat B5, Golf IV |
| IMMO3 Motorola | Motorola | Golf IV, Seat, Skoda |
| IMMO3 NEC | NEC | Audi, VW |
| IMMO4 Kayaba | Kayaba | A8, Touareg, Cayenne (2003+) |

## Лицензия

MIT License
