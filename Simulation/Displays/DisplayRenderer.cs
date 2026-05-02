using System;
using System.Windows.Media;

namespace VagImmoEditor.Pro.Simulation.Displays
{
    /// <summary>
    /// Интерфейс рендерера дисплея
    /// </summary>
    public interface IDisplayRenderer
    {
        void SetDisplayType(DisplayType type);
        void Render(DashboardState state);
        void Clear();
        double GetBrightness();
        void SetBrightness(double value);
    }

    /// <summary>
    /// Рендерер дисплея с поддержкой разных типов
    /// </summary>
    public class DisplayRenderer : IDisplayRenderer
    {
        private DisplayType _currentType;
        private double _brightness = 1.0;

        public void SetDisplayType(DisplayType type)
        {
            _currentType = type;
            Clear();
        }

        public void Render(DashboardState state)
        {
            // Рендеринг в зависимости от типа дисплея
            switch (_currentType)
            {
                case DisplayType.Segment:
                    RenderSegmentDisplay(state);
                    break;
                case DisplayType.Fishtank:
                    RenderFishtankDisplay(state);
                    break;
                case DisplayType.DotMatrix:
                    RenderDotMatrixDisplay(state);
                    break;
                case DisplayType.ColorTft:
                    RenderColorTftDisplay(state);
                    break;
                case DisplayType.Oled:
                    RenderOledDisplay(state);
                    break;
            }
        }

        private void RenderSegmentDisplay(DashboardState state)
        {
            // 7-сегментный дисплей (IMMO2, старые Golf/Bora)
            // Показывает только пробег и простые сообщения
            // Симуляция сегментов: каждый сегмент имеет яркость 0-100%
        }

        private void RenderFishtankDisplay(DashboardState state)
        {
            // VDO "аквариум" (Passat B5, Golf IV, Audi A3/A4)
            // Характерная голубая подсветка, изогнутые линии
            // Поддержка многострочных сообщений
            
            var message = FormatMessage(state.DisplayMessage, 16);
            var mileage = state.Mileage.ToString("F0").PadLeft(6, ' ');
        }

        private void RenderDotMatrixDisplay(DashboardState state)
        {
            // Точечная матрица (Motorola, NEC)
            // Более гибкое отображение графики
            // Поддержка простых иконок
            
            var time = state.CurrentTime.ToString("HH:mm");
            var temp = $"{state.OutsideTemp:F0}°C";
        }

        private void RenderColorTftDisplay(DashboardState state)
        {
            // Цветной TFT (IMMO4, современные приборы)
            // Полноцветная графика, меню, навигация
            
            if (state.ComponentProtection)
            {
                // Показать предупреждение о защите компонента
            }
            
            if (!state.ImmoActive)
            {
                // Показать статус иммобилайзера
            }
        }

        private void RenderOledDisplay(DashboardState state)
        {
            // OLED (новейшие модели)
            // Высокий контраст, глубокий черный цвет
            // Анимированные переходы
        }

        private string FormatMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            return message.Length > maxLength ? message.Substring(0, maxLength) : message.PadRight(maxLength);
        }

        public void Clear()
        {
            // Очистка дисплея
        }

        public double GetBrightness() => _brightness;

        public void SetBrightness(double value)
        {
            _brightness = Math.Max(0.0, Math.Min(1.0, value));
        }
    }

    /// <summary>
    /// Тип дисплея приборной панели
    /// </summary>
    public enum DisplayType
    {
        Segment,      // 7-сегментный (старые модели)
        Fishtank,     // VDO "аквариум" (Golf IV, Passat B5)
        DotMatrix,    // Точечная матрица (Motorola/NEC)
        ColorTft,     // Цветной TFT (IMMO4+)
        Oled          // OLED (новейшие)
    }

    /// <summary>
    /// Палитры цветов для разных типов дисплеев
    /// </summary>
    public static class DisplayPalettes
    {
        // VDO Fishtank - характерная голубая подсветка
        public static readonly Color FishtankBlue = Color.FromRgb(0, 180, 255);
        public static readonly Color FishtankDark = Color.FromRgb(0, 50, 100);
        
        // Segment - красный/оранжевый
        public static readonly Color SegmentRed = Color.FromRgb(255, 50, 0);
        public static readonly Color SegmentOrange = Color.FromRgb(255, 150, 0);
        
        // DotMatrix - зеленый/янтарный
        public static readonly Color DotMatrixGreen = Color.FromRgb(0, 255, 100);
        public static readonly Color DotMatrixAmber = Color.FromRgb(255, 200, 0);
        
        // TFT - полноцветная
        public static readonly Color TftWhite = Colors.White;
        public static readonly Color TftBlack = Colors.Black;
        public static readonly Color TftWarning = Colors.Orange;
        public static readonly Color TftError = Colors.Red;
        
        // OLED - высокий контраст
        public static readonly Color OledPureBlack = Colors.Black;
        public static readonly Color OledBrightWhite = Color.FromRgb(255, 255, 255);
    }
}
