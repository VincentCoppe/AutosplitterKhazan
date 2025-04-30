using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AutoSplitterKhazan
{
    class Program
    {
        private static readonly string[] Bosses = new string[] {
        "CB_Yetuga",
        "CB_BladePhantom",
        "CB_Dragonian_TwinBlade_Boss_Phase2",
        "CB_04_Bolvino",
        "CB_Aratra",
        "CB_06_Rangkus",
        "CB_07_Maluca",
        "CB_Elamein",
        "CB_Shactuka",
        "CB_10_Troka",
        "CB_11_Bellerian",
        "CB_12_Skalpel",
        "CB_OnycsBlack",
        "CB_14_Hismar",
        "CB_15_Reese_Phase2"};

        private static readonly string endOfGame = new string ("FileName:LOB_OzmaI_Frame");
        private static readonly string endCutscene = "HiddenState:Cinematic";
        private static readonly string crevasse = "LogWorld: SeamlessTravel to: /Game/_Kazan_/Level/TheCrevice";
        private static readonly string NotThecrevasse = @"LogWorld: SeamlessTravel to: \/Game\/_Kazan_\/Level\/(?!.*?(TheCrevice)).+";
        private static readonly string mainMenu = "BBQ: Portal Process AnotherWorld - TargetLevel:/Game/_Kazan_/Level/System/Lobby/LB_LobbyLevel";     
        private static readonly string controlThePlayer = "BBQ: UxxInputLockManager::ResetAllLockInput InReason:Loading";

        private static bool hasreachedend = false;
        private static bool InTheCrevasse = false;
        private static bool TimerStarted = false;
        private static bool InTheMainMenu = false;
        private static bool InLoadingScreen = false;


       


        // Win32 API function to read OutputDebugString
        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes, uint flProtect, uint dwMaximumSizeHigh,
            uint dwMaximumSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh,
            uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        const uint FILE_MAP_READ = 0x0004;
        const string DBWIN_BUFFER = "DBWIN_BUFFER";
        const string DBWIN_BUFFER_READY = "DBWIN_BUFFER_READY";
        const string DBWIN_DATA_READY = "DBWIN_DATA_READY";

        static void Main(string[] args)
        {
            Console.WriteLine("AutoSplitter started. Waiting for boss kills...");

            System.Threading.Thread thread = new System.Threading.Thread(ListenToDebugOutput);
            thread.IsBackground = true;
            thread.Start();

            Console.ReadLine(); // Keep the main thread alive
        }

        static void ListenToDebugOutput()
        {
            var dbWinReadyEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, DBWIN_BUFFER_READY);
            var dbWinDataEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, DBWIN_DATA_READY);

            var hFileMapping = OpenFileMapping(FILE_MAP_READ, false, DBWIN_BUFFER);
            if (hFileMapping == IntPtr.Zero)
            {
                hFileMapping = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04 /* PAGE_READWRITE */, 0, 4096, DBWIN_BUFFER);
                if (hFileMapping == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to create DBWIN_BUFFER. Error: " + GetLastError());
                    return;
                }
            }

            var pBuf = MapViewOfFile(hFileMapping, FILE_MAP_READ, 0, 0, 4096);
            if (pBuf == IntPtr.Zero)
            {
                Console.WriteLine("Cannot map DBWIN_BUFFER.");
                return;
            }

            // Tell the system we are READY first!
            dbWinReadyEvent.Set();

            while (true)
            {
                dbWinDataEvent.WaitOne(); // Wait for new data

                string? output = Marshal.PtrToStringAnsi(pBuf + 4); // Skip PID

                if (!string.IsNullOrEmpty(output))
                {

                    //Check if we are in the crevice or in a level also tell us that we are in a loading screen
                    if (output.Contains(crevasse)){
                        InTheMainMenu = false;
                        InTheCrevasse = true;
                        
                        InLoadingScreen = true;
                        //if (TimerStarted) SendCommand("pausegametime");
                    } 
                    if (Regex.Match(output, NotThecrevasse).Success)
                    {
                        InTheMainMenu = false;
                        InTheCrevasse = false;
                        
                        InLoadingScreen = true;
                       // if (TimerStarted) SendCommand("pausegametime");
                    }

                    //Check If we go back to the mainMenu 
                    if (output.Contains(mainMenu))
                    {
                        
                        InTheMainMenu = true;
                        SendCommand("reset");
                        TimerStarted = false;
                    }

                    //when we get the control of the player It mean we aren't in a loading screen anymore
                    if (output.Contains(controlThePlayer))
                    {
                        
                        if (InLoadingScreen && TimerStarted)
                        {
                          //  SendCommand("unpausegametime");
                        }
                        InLoadingScreen = false;
                        //If we aren't in the Crevice when We get control of the characters then we can start the timer
                        if (!InTheCrevasse && !TimerStarted)
                        {
                            
                            SendCommand("setgametime 0:00:00.000");
                            SendCommand("switchto gametime");
                            SendCommand("start");
                            TimerStarted = true;
                        }
                    }

                    //Split At the defeat of every boss except ozma
                    foreach (string boss in Bosses)
                    {
                        if (output.Contains("BBQ: OnDie") && output.Contains(boss))
                        {
                            
                            
                            SendCommand($"setgametime {SendGetCommand("getcurrentgametime")}");
                            SendCommand("split");
                            break;
                        }
                    }


                    // Check if we reached the end of the game
                    if (output.Contains("BBQ: EndInteraction") && output.Contains(endOfGame))
                    {
                        hasreachedend = true;
                    }
                    if (hasreachedend)
                    {
                        if (output.Contains(endCutscene))
                        {
                            hasreachedend = false;
                            
                            SendCommand($"setgametime {SendGetCommand("getcurrentgametime")}");
                            SendCommand("split");
                        }
                    }

                    


                }
                // VERY IMPORTANT: Immediately say we are READY again
                dbWinReadyEvent.Set();
            }
        }

        static string SendGetCommand(string command)
        {
            try
            {
                using (TcpClient client = new TcpClient("127.0.0.1", 16834))
                using (StreamWriter writer = new StreamWriter(client.GetStream()))
                using (StreamReader reader = new StreamReader(client.GetStream()))
                {
                    writer.WriteLine(command);
                    writer.Flush();
                    var answer = reader.ReadLine();                   
                    return (answer!=null) ? answer : "";
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send split command: {ex.Message}");
                return $"Failed to send split command: {ex.Message}";
            }
        }

        static void SendCommand(string command)
        {
            try
            {
                using (TcpClient client = new TcpClient("127.0.0.1", 16834))
                using (StreamWriter writer = new StreamWriter(client.GetStream()))
                {
                    writer.WriteLine(command);
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send split command: {ex.Message}");
            }
        }




    }
}
