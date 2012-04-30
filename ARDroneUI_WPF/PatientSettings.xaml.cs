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
    public partial class PatientSettings : Window
    {
        public PatientSettings()
        {
            InitializeComponent();

            SearchPatients("");
        }

        public void SearchPatients(String term)
        {
            Queue<SearchResult> results = Input.InputManager.DatabaseController.SearchPatients(term);

            SearchResults.Items.Clear();

            foreach (SearchResult r in results)
            {
                ListBoxItem i = new ListBoxItem();
                i.Tag = r.ID;
                i.Content = r.Name;

                SearchResults.Items.Add(i);
            }
        }

        private void SearchCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SearchSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (PatientID.Text != "")
            {
                Input.InputManager.DatabaseController.getPatientByID(int.Parse(PatientID.Text));
                this.Close();
            }
            else
            {
                ListBoxItem selected = (ListBoxItem)SearchResults.SelectedItem;
                Input.InputManager.DatabaseController.getPatientByID(((Int32)(selected.Tag)));
                this.Close();
            }
        }

        private void PatientName_KeyUp(object sender, KeyEventArgs e)
        {
            SearchPatients(PatientName.Text);
        }
    }
}
