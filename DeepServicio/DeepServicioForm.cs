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
        bool ciclos = true;
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

                    //iniciarConexionHilo.Abort();
                    clientSocket.Close();
                    ciclos = false;
                    process = Process.GetProcessesByName(processName).FirstOrDefault();
                    process.WaitForExit();
                    // Process.Start("shutdown", $"/r /f /t 0");
                    MessageBox.Show("Apagar");
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

  
        public void IniciarConexion()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(serverIP), port);
           
            while (ciclos)
            {
                try
                {
                    clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    while (ciclos)
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
                    string clientMessage = ObtenerNombreEquipo() + "|" + ObtenerGrupoDeTrabajo() + "|" + "Sin iniciar" + "|" + n_inventario;
                    byte[] clientMessageBytes = Encoding.ASCII.GetBytes(clientMessage);
                    clientSocket.Send(clientMessageBytes);
        
                    while (ciclos)
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
