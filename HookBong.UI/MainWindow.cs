﻿using HookBong.Core;
using HookBong.Core.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HookBong.UI
{
    public partial class MainWindow : Form
    {
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process([In] IntPtr processHandle, [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

        public List<Process> Processes = new List<Process>();

        public bool ProcessFilter(Process p)
        {
            try
            {
                var hdl =  MemoryReader.OpenProcess(MemoryReader.ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
                if (hdl == IntPtr.Zero)
                    return false;
                if (!Environment.Is64BitOperatingSystem)
                    return true;
                IsWow64Process(hdl, out bool rv);
                MemoryReader.CloseHandle(hdl);
                return rv == !Environment.Is64BitProcess;
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public void RefreshProcesses()
        {
            Processes.Clear();
            processList.Items.Clear();

            var ps = Process.GetProcesses();
            foreach (var p in ps)
            {
                if (p.ProcessName.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) == -1)
                    continue;

                if (!ProcessFilter(p))
                    continue;

                Processes.Add(p);
            }

            foreach (var process in Processes)
                processList.Items.Add($"{process.ProcessName} [{process.Id}]");
        }

        protected override void OnResizeBegin(EventArgs e) {
            SuspendLayout();
            base.OnResizeBegin(e);
        }
        protected override void OnResizeEnd(EventArgs e) {
            ResumeLayout();
            base.OnResizeEnd(e);
        }

        public MainWindow()
        {
            InitializeComponent();
            RefreshProcesses();
        }

        private Dictionary<int, List<HookAnalysisResult>> _cachedAnalyses = new Dictionary<int, List<HookAnalysisResult>>();

        private void processList_SelectedIndexChanged(object sender, EventArgs e)
        {
            analyzeButton.Enabled = true;

            analysisGrid.Rows.Clear();

            var pid = int.Parse(processList.SelectedItem.ToString().Split('[', ']')[1]);

            if (_cachedAnalyses.ContainsKey(pid)) //load cached analysis
            {
                foreach (var entry in _cachedAnalyses[pid])
                    analysisGrid.Rows.Add(entry.Location, entry.ModuleName, entry.Type, entry.OriginalData, entry.PatchedData, entry.AdditionalInfo);
            }

            currentProcessLabel.Text = $@"Current Process: {((ListBox)sender).SelectedItem}";
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            RefreshProcesses();
        }


        private void analyzeButton_Click(object sender, EventArgs e)
        {
            analysisGrid.Rows.Clear();
            var targetProcess = Processes.First(p => p.Id == int.Parse(processList.SelectedItem.ToString().Split('[', ']')[1])); //kinda yikes ngl
            var analysisEngine = new ProcessAnalyzer(targetProcess);
            
            var ana = analysisEngine.AnalyzeFull();


            if (_cachedAnalyses.ContainsKey(targetProcess.Id)) //always add latest analysis
                _cachedAnalyses.Remove(targetProcess.Id);
            

            _cachedAnalyses.Add(targetProcess.Id,ana);

            foreach (var entry in ana)
                analysisGrid.Rows.Add(entry.Location, entry.ModuleName, entry.Type, entry.OriginalData, entry.PatchedData, entry.AdditionalInfo);
        }

        private void Searchbox_textChanged(object sender, EventArgs e)
        {
            processList.Items.Clear();

            foreach (var p in Processes.Where(p => p.ProcessName.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) != -1).Where(ProcessFilter))
            {
                processList.Items.Add($"{p.ProcessName} [{p.Id}]");
            }

        }
    }
}