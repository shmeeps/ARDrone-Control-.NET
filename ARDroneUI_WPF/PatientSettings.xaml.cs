using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ARDrone.Input;
using ARDrone.Input.Utils;

namespace ARDrone.UI
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
        }

        public void SearchPatients(String term)
        {
            // TODO: Fix Databasecontroller scope?
            // Queue<Input.InputManager.DatabaseController.> result = Input.InputManager.DatabaseController.SearchPatients(term);
        }

        private void SearchCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SearchSubmit_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Check to see if any patient selected in list box, if not, check for patient id, if not, fail
        }

        private void PatientName_KeyUp(object sender, KeyEventArgs e)
        {
            SearchPatients(PatientName.Text);
        }
    }
}
