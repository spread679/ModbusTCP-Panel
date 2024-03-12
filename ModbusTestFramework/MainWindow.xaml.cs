using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ModbusTestFramework
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        EasyModbus.ModbusClient _modbusClient = null;
        System.Threading.Thread _thread = null;

        public enum DataType
        {
            Short,
            Int,
            Float,
            Long,
            String,
            F16,
        }

        public MainWindow()
        {
            InitializeComponent();
        }


        public class DataItem
        {
            public int Address { get; set; }
            public object Value { get; set; }
            
        }

        private void insertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)HoldingRadioButton.IsChecked)
                {
                    if ((DataType)this.SelectedTypeComboBox.SelectedItem == DataType.Short)
                    {
                        int address = int.Parse(this.AddressTextBox.Text);
                        int value = int.Parse(this.ValueTextBox.Text);
                        _modbusClient.WriteSingleRegister(address, value);
                    }
                    else if ((DataType)this.SelectedTypeComboBox.SelectedItem == DataType.String)
                    {
                        int address = int.Parse(this.AddressTextBox.Text);
                        int[] arrString = EasyModbus.ModbusClient.ConvertStringToRegisters(this.ValueTextBox.Text);
                        _modbusClient.WriteMultipleRegisters(address, arrString);
                    }
                    else if ((DataType)this.SelectedTypeComboBox.SelectedIndex == DataType.F16)
                    {
                        int address = int.Parse(this.AddressTextBox.Text);
                        string[] splittedValues = this.ValueTextBox.Text.Split('|');
                        int size = int.Parse(this.SizeTextBox.Text);

                        if (splittedValues.Length < size)
                            size = splittedValues.Length;

                        int[] values = new int[size];


                        for (int i = 0; i < size; i++)
                        {
                            values[i] = int.Parse(splittedValues[i]);
                        }

                        _modbusClient.WriteMultipleRegisters(address, values);
                    }
                    else
                    {
                        MessageBox.Show("Not configured");
                    }

                }
                else if ((bool)CoilsRadioButton.IsChecked)
                {
                    int address = int.Parse(this.AddressTextBox.Text);
                    bool value = bool.Parse(this.ValueTextBox.Text);
                    MessageBox.Show($"Before sending {value}");
                    //_modbusClient.WriteSingleCoil(address, value);
                    _modbusClient.WriteMultipleCoils(address, new bool[] { value });
                }

            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!this.Connected())
                {
                    string ip = this.IPTextBox.Text;
                    bool validPort = int.TryParse(this.PortTextBox.Text, out int port);
                    bool validTimeout = int.TryParse(this.TimeoutTextBox.Text, out int timeout);
                    bool hasUserID = (bool)this.HasUserIDCheckBox.IsChecked;

                    if ((validPort && (port > 0 && port < 65535)) && validTimeout)
                    {
                        _modbusClient = new EasyModbus.ModbusClient(ip, port)
                        {
                            ConnectionTimeout = timeout,
                        };

                        if (hasUserID)
                        {
                            bool validUserID = byte.TryParse(this.UserIDTextBox.Text, out byte userID);
                            if (validUserID)
                            {
                                _modbusClient.UnitIdentifier = userID;
                            }
                            else
                            {
                                throw new ArgumentException("Invalid user ID.");
                            }
                        }

                        _modbusClient.LogFileFilename = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test.txt");
                        _modbusClient.connectedChanged += _modbusClient_connectedChanged;

                        _modbusClient.Connect();
                    }
                    else
                    {
                        throw new ArgumentException("Invalid port or connection timeout.");
                    }
                }
                else
                {
                    _modbusClient.Disconnect();
                    this.connectButton.Content = "Connect";
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void _modbusClient_connectedChanged(object sender)
        {
            if (_modbusClient.Connected)
            {
                this.connectButton.Content = "Disconnect";

                this.IPTextBox.IsEnabled = false;
                this.PortTextBox.IsEnabled = false;
                this.TimeoutTextBox.IsEnabled = false;
                this.HasUserIDCheckBox.IsEnabled = false;
                this.UserIDTextBox.IsEnabled = false;

                this.insertButton.IsEnabled = true;
                this.readButton.IsEnabled = true;
            }
            else
            {
                this.connectButton.Content = "Connect";

                this.IPTextBox.IsEnabled = true;
                this.PortTextBox.IsEnabled = true;
                this.TimeoutTextBox.IsEnabled = true;
                this.HasUserIDCheckBox.IsEnabled = true;
                this.UserIDTextBox.IsEnabled = true;

                this.insertButton.IsEnabled = false;
                this.readButton.IsEnabled = false;

            }
        }

        private void poolingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.readButton.Content.ToString() == "Start pooling")
                {
                    _thread = new System.Threading.Thread(() =>
                    {
                        try
                        {
                            while (true)
                            {
                                this.ReadModbusTCP();
                                System.Threading.Thread.Sleep(10000);
                            }
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                    });

                    _thread.Start();
                    this.readButton.Content = "Stop pooling";
                }
                else if (_thread != null)
                {
                    _thread.Abort();
                    this.readButton.Content = "Start pooling";
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.Connected())
                    _modbusClient.Disconnect();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// Control if is already connected to the machine
        /// </summary>
        /// <returns></returns>
        private bool Connected()
        {
            return _modbusClient != null && _modbusClient.Connected;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                this.SelectedTypeComboBox.ItemsSource = Enum.GetValues(typeof(DataType));
                this.SelectedTypeComboBox.SelectedItem = DataType.Short;

                this.IPTextBox.IsEnabled = true;
                this.PortTextBox.IsEnabled = true;
                this.TimeoutTextBox.IsEnabled = true;
                this.HasUserIDCheckBox.IsEnabled = true;
                this.UserIDTextBox.IsEnabled = true;

                this.insertButton.IsEnabled = false;
                this.readButton.IsEnabled = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void dg_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var ok = "";
        }

        /// <summary>
        /// An helper to read all the datas in ModbusTCP client
        /// </summary>
        private void ReadModbusTCP()
        {
            int size = 1;
            bool holdingChecked = false;
            bool coilChecked = false;
            bool inputChecked = false;
            this.SizeTextBox.Dispatcher.Invoke((Action)(() =>
            {
                size = int.Parse(this.SizeTextBox.Text);

            }));
            this.HoldingRadioButton.Dispatcher.Invoke((Action)(() =>
            {
                holdingChecked = (bool)this.HoldingRadioButton.IsChecked;
            }));
            this.CoilsRadioButton.Dispatcher.Invoke((Action)(() =>
            {
                coilChecked = (bool)this.CoilsRadioButton.IsChecked;
            }));
            this.InputRadioButton.Dispatcher.Invoke((Action)(() =>
            {
                inputChecked = (bool)this.InputRadioButton.IsChecked;
            }));
            this.dg.Dispatcher.Invoke((Action)(() =>
            {
                this.dg.Items.Clear();

            }));

            if (holdingChecked)
            {
                int address = 0;
                DataType selectedType = DataType.Short;
                
                this.AddressTextBox.Dispatcher.Invoke((Action)(() =>
                {
                    address = int.Parse(this.AddressTextBox.Text);
                }));
                this.SelectedTypeComboBox.Dispatcher.Invoke((Action)(() =>
                {
                    selectedType = (DataType)this.SelectedTypeComboBox.SelectedItem;
                }));

                var readHoldingRegister = _modbusClient.ReadHoldingRegisters(address, size);

                if (selectedType == DataType.Short)
                {
                    for (int i = 0; i < size; i++)
                    {
                        dg.Dispatcher.Invoke((Action)(() =>
                        {
                            dg.Items.Add(new DataItem { Address = address + i, Value = readHoldingRegister[i] });

                        }));
                    }
                }
                else if (selectedType == DataType.Int)
                {
                    int intValue = EasyModbus.ModbusClient.ConvertRegistersToInt(readHoldingRegister);
                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = intValue });

                    });
                }
                else if (selectedType == DataType.Float)
                {
                    float floatValue = EasyModbus.ModbusClient.ConvertRegistersToFloat(readHoldingRegister);
                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = floatValue });

                    });
                }
                else if (selectedType == DataType.String)
                {

                    string stringtValue = EasyModbus.ModbusClient.ConvertRegistersToString(readHoldingRegister, 0, size);
                    stringtValue.Replace("\0", "");
                    string clearString = "";

                    for (int i = 0; i < stringtValue.Length; i++)
                    {
                        char character = stringtValue[i];
                        if ((int)character == 0)
                            continue;
                        clearString += character;
                    }

                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = clearString });

                    });

                }
                else if (selectedType == DataType.Long)
                {
                    long longValue = EasyModbus.ModbusClient.ConvertRegistersToLong(readHoldingRegister);

                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = longValue });

                    });
                }
                else
                {
                    MessageBox.Show("Not configured");
                }

            }
            else if (coilChecked)
            {
                int address = int.Parse(this.AddressTextBox.Text);
                var coilsRegister = _modbusClient.ReadCoils(address, size);

                for (int i = 0; i < size; i++)
                {
                    dg.Items.Add(new DataItem { Address = address, Value = coilsRegister[i] });
                }
            }
            else if (inputChecked)
            {
                int address = 0;
                DataType selectedType = DataType.Short;

                this.AddressTextBox.Dispatcher.Invoke((Action)(() =>
                {
                    address = int.Parse(this.AddressTextBox.Text);
                }));
                this.SelectedTypeComboBox.Dispatcher.Invoke((Action)(() =>
                {
                    selectedType = (DataType)this.SelectedTypeComboBox.SelectedItem;
                }));

                var readInputRegister = _modbusClient.ReadInputRegisters(address, size);

                if (selectedType == DataType.Short)
                {
                    for (int i = 0; i < size; i++)
                    {
                        dg.Dispatcher.Invoke((Action)(() =>
                        {
                            dg.Items.Add(new DataItem { Address = address + i, Value = readInputRegister[i] });

                        }));
                    }
                }
                else if (selectedType == DataType.Int)
                {
                    int intValue = EasyModbus.ModbusClient.ConvertRegistersToInt(readInputRegister);
                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = intValue });

                    });
                }
                else if (selectedType == DataType.Float)
                {
                    float floatValue = EasyModbus.ModbusClient.ConvertRegistersToFloat(readInputRegister);
                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = floatValue });

                    });
                }
                else if (selectedType == DataType.String)
                {

                    string stringtValue = EasyModbus.ModbusClient.ConvertRegistersToString(readInputRegister, 0, size);
                    stringtValue.Replace("\0", "");
                    string clearString = "";

                    for (int i = 0; i < stringtValue.Length; i++)
                    {
                        char character = stringtValue[i];
                        if ((int)character == 0)
                            continue;
                        clearString += character;
                    }

                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = clearString });

                    });

                }
                else if (selectedType == DataType.Long)
                {
                    long longValue = EasyModbus.ModbusClient.ConvertRegistersToLong(readInputRegister);

                    dg.Dispatcher.Invoke(() =>
                    {
                        dg.Items.Add(new DataItem { Address = address, Value = longValue });

                    });
                }
                else
                {
                    MessageBox.Show("Not configured");
                }

            }
        }

        private void readButton_Click(object sender, RoutedEventArgs e)
        {
            checked
            {
                try
                {
                    this.ReadModbusTCP();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            
        }
    }
}
