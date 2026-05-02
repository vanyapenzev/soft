using System.Windows.Controls;
using VagImmoEditor.Pro.UI.ViewModels;

namespace VagImmoEditor.Pro.UI.Views
{
    /// <summary>
    /// Логика взаимодействия для Dashboard3DView.xaml
    /// </summary>
    public partial class Dashboard3DView : UserControl
    {
        private readonly DashboardSimulationViewModel _viewModel;

        public Dashboard3DView()
        {
            InitializeComponent();
            
            _viewModel = new DashboardSimulationViewModel();
            DataContext = _viewModel;
            
            // Настройка 3D viewport
            SetupViewport();
        }

        private void SetupViewport()
        {
            // Начальная настройка камеры
            if (DashboardViewport.Camera is System.Windows.Media.Media3D.PerspectiveCamera camera)
            {
                camera.Position = new System.Windows.Media.Media3D.Point3D(0, 0, 800);
                camera.LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, -1);
                camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                camera.FieldOfView = 45;
            }
        }

        /// <summary>
        /// Инициализация симуляции с данными EEPROM
        /// </summary>
        public void InitializeFromEeprom(byte[] eepromData)
        {
            _viewModel.InitializeFromEeprom(eepromData);
        }

        protected override void OnUnloaded(System.Windows.RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            _viewModel.Dispose();
        }
    }
}
