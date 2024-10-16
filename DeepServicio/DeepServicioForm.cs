using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using System.Management;
using System.Net.NetworkInformation;


namespace DeepServicio
{
    public partial class DeepServicio : Form
    {
        

        string processName = "DeepControl";
        Process process = null;
        string VersionDeep = "v.0.2.3";
        string serverIP = "192.168.0.172";//"172.16.42.200";
        string fecha_compilado = "07_Octubre_2024";
        string status_licencia = "Activa";
        string n_inventario = "N/A";
        int port = 47373;
        int delay_conexion = 2000; //retraso entre espera de conexiones
        static int delay_captura = 1000;
        Socket clientSocket = null;
        public enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001, SHERB_NOPROGRESSUI = 0x00000002, SHERB_NOSOUND = 0x00000004
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);
        private NotifyIcon notifyIcon;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;
        public DeepServicio()
        {
            InitializeComponent();
            
            Thread iniciarConexionHilo = new Thread(IniciarConexion);
            iniciarConexionHilo.Start();
            
            while (true)
            {
                if (IsProcessRunning(processName))
                {

                    iniciarConexionHilo.Abort();
                    clientSocket.Close();
                    process = Process.GetProcessesByName(processName).FirstOrDefault();
                    process.WaitForExit();
                    Process.Start("shutdown", $"/r /t 0");
                    break;
                }
                Thread.Sleep(1000);
            }
            Application.Exit();
            Environment.Exit(0);
        }
        public bool IsProcessRunning(string processName)
        {
            // Obtener todos los procesos con el nombre dado
            Process[] processes = Process.GetProcessesByName(processName);

            // Si la lista de procesos no está vacía, significa que el proceso está en ejecución
            return processes.Length > 0;
        }
        static void ApagarEquipo()
        {
            Process.Start(new ProcessStartInfo("shutdown", "/s /f /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        static void ReiniciarEquipo()
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /f /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        static string GetAllLocalIPAddresses()
        {
            string localIPs = string.Empty;
            string hostName = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);

            foreach (IPAddress ip in addresses)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) // Filtrar solo direcciones IPv4
                {
                    localIPs += ip.ToString() + ", "; // Agregar IP a la cadena
                }
            }

            // Eliminar la última coma y espacio si hay direcciones IP
            if (localIPs.Length > 0)
            {
                localIPs = localIPs.TrimEnd(',', ' ');
            }

            return localIPs; // Retornar todas las IPs en una sola cadena
        }

        public string ObtenerGrupoDeTrabajo()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    // Verificar si el objeto tiene la propiedad "Workgroup"
                    if (obj["Workgroup"] != null)
                    {
                        // Si existe, retornar su valor convertido a cadena
                        return obj["Workgroup"].ToString();
                    }
                    else
                    {
                        // Si no existe, retornar un valor predeterminado
                        return Environment.UserDomainName;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    return Environment.UserDomainName;
                }
                catch (Exception ex1)
                {
                    return "N/A";
                }

            }
            return "Grupo de trabajo no encontrado";
        }
        public string ObtenerNombreEquipo()
        {
            string[] partes;
            string nombreEquipo = "";
            try
            {
                nombreEquipo = Environment.MachineName;
                if (nombreEquipo.Contains("-"))
                {
                    partes = nombreEquipo.Split('-');
                    nombreEquipo = partes[0];
                    n_inventario = partes[1];
                }
            }
            catch (Exception ex)
            {
                return "N/A";


            }
            return nombreEquipo;
        }

        public string GetEthernetIPAddressAndMAC()
        {
            // Obtener todos los adaptadores de red
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Verificar si el adaptador está activo y es de tipo Ethernet
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    // Obtener las direcciones IP asociadas al adaptador
                    foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                    {
                        // Retornar la primera dirección IPv4
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            // Obtener la dirección MAC
                            string macAddress = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());
                            return (unicast.Address.ToString() + "|" + macAddress); // Retornar IP y MAC
                        }
                    }
                }
            }
            return "N/A|N/A"; // Si no se encuentra ninguna dirección IP ni MAC
        }
        public string GetWiFiIPAddressAndMAC()
        {
            // Obtener todos los adaptadores de red
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Verificar si el adaptador está activo y es de tipo Wi-Fi
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    // Obtener las direcciones IP asociadas al adaptador
                    foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                    {
                        // Retornar la primera dirección IPv4
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            // Obtener la dirección MAC
                            string macAddress = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());
                            return (unicast.Address.ToString() + "|" + macAddress); // Retornar IP y MAC
                        }
                    }
                }
            }
            return "N/A|N/A"; // Si no se encuentra ninguna dirección IP ni MAC
        }
        static string ObtenerInformacionRAM()
        {
            string resultado = "N/A";
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                foreach (ManagementObject ram in searcher.Get())
                {
                    string tipoRAM = ram["Manufacturer"]?.ToString() + " " + ram["Speed"]?.ToString() + " MHz";
                    ulong capacidadRAM = (ulong)ram["Capacity"] / (1024 * 1024 * 1024); // Convertir a GB
                    resultado = capacidadRAM + " GB - Modelo: " + tipoRAM;
                }
                return resultado;
            }
            catch (Exception ex)
            {
                return resultado;
            }
        }
        static string ObtenerInformacionSistemaOperativo()
        {
            string resultado = "N/A";
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    string nombreSO = os["Caption"]?.ToString();
                    string versionSO = os["Version"]?.ToString();
                    resultado = nombreSO + " " + versionSO;
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return resultado;
            }
        }
        static string ObtenerInformacionProcesador()
        {
            string resultado = "N/A";
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject procesador in searcher.Get())
                {
                    // Obtener el nombre del procesador
                    string nombreProcesador = procesador["Name"]?.ToString();
                    // Obtener la velocidad del procesador
                    string velocidadProcesador = procesador["MaxClockSpeed"]?.ToString() + " MHz";

                    resultado += $"Nombre del Procesador: {nombreProcesador}\n";
                    resultado = nombreProcesador + " " + velocidadProcesador;
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return resultado;
            }
        }
        static string ObtenerInformacionDiscosDuros()
        {
            string resultado = "";
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject disco in searcher.Get())
                {
                    ulong capacidadDisco = (ulong)disco["Size"] / (1024 * 1024 * 1024);
                    string tipoDisco = disco["MediaType"]?.ToString() ?? "N/A";
                    resultado += $"{capacidadDisco} GB - {tipoDisco} ";
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return "N/A";
            }
        }
        static string ObtenerMarcaYModelo()
        {
            string marca = "N/A";
            string modelo = "N/A";
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (ManagementObject computer in searcher.Get())
                {
                    marca = computer["Manufacturer"]?.ToString() ?? "N/A";
                    modelo = computer["Model"]?.ToString() ?? "N/A";
                }
            }
            catch (Exception ex)
            {
                return marca + "|" + modelo;
            }

            return marca + "|" + modelo;
        }



        public string GetPublicIPAddress()
        {
            try
            {
                // Realiza una solicitud a un servicio que devuelve la IP pública
                using (WebClient client = new WebClient())
                {
                    // Obtiene la IP desde un servicio externo
                    return client.DownloadString("http://api.ipify.org").Trim();
                }
            }
            catch (Exception ex)
            {

                return "N/A";
            }
        }
        public static string ObtenerHoraActual()
        {
            // Obtener la hora actual y formatearla como "HH:mm:ss"
            return DateTime.Now.ToString("HH:mm:ss");
        }

        public void IniciarConexion()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(serverIP), port);
           
            while (true)
            {
                try
                {
                    clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    while (true)
                    {
                        try
                        {
                            clientSocket.Connect(endPoint);
                            break;
                        }
                        catch (SocketException ex)
                        {
                            Thread.Sleep(delay_conexion);
                        }
                    }
                    string clientMessage = ObtenerNombreEquipo() + "|" + ObtenerGrupoDeTrabajo() + "|" + "sin" + "|" + n_inventario + "|" + GetEthernetIPAddressAndMAC() + "|" + GetWiFiIPAddressAndMAC() + "|" + GetPublicIPAddress() + "|" + ObtenerInformacionRAM() + "|" + ObtenerInformacionSistemaOperativo() + "|" + ObtenerInformacionProcesador() + "|" + ObtenerInformacionDiscosDuros() + "|" + ObtenerMarcaYModelo() + "|" + "N/A" + "|" + "Seed de servicio" + "|" + "N/A" + "|" + ObtenerHoraActual();
                    byte[] clientMessageBytes = Encoding.ASCII.GetBytes(clientMessage);
                    clientSocket.Send(clientMessageBytes);
        
                    while (true)
                    {
                        byte[] buffer = new byte[1024];
                        int receivedBytes = clientSocket.Receive(buffer);
                        string serverMessage = Encoding.ASCII.GetString(buffer, 0, receivedBytes);
                        
                        if (serverMessage == "apagar")
                        {
                            ApagarEquipo();
                        }
                        if (serverMessage == "reiniciar")
                        {
                            ReiniciarEquipo();
                        }
                        
                    }
                }
                catch (SocketException ex)
                {
                    Thread.Sleep(delay_conexion);
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    clientSocket.Close();

                }

            }
        }
    }
}
