using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Daugiagijis._2PD
{
    public partial class Form1 : Form
    {
        byte[] keyValue = Encoding.ASCII.GetBytes("~!@#$%^&*()_+=-0987654321`[],;'/");
        byte[] IV_Value = Encoding.ASCII.GetBytes("<>?:{}|ASDJNGHFY");
        private List<string> hashListOfEncrypted = new List<string>();
        private List<string> hashListOfDecrypted = new List<string>();
        
        private int barLenght = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void ChooseDirectoryButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                PathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void EncryptButton_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(PathTextBox.Text) || Directory.Exists(PathTextBox.Text) == false)
            {
                MessageBox.Show("Directory path is not valid!");
            }
            else
            {
                string path = PathTextBox.Text;
                Thread th1 = new Thread(() => encryptThread(path));
                th1.Name = "Encryption thread";
                th1.Start();
            }
        }

        private void encryptThread(string path)
        {
            resetParameters();
            try
            {
                countProgressBarLenght(path);
                Console.WriteLine(barLenght);
                Invoke((Action)delegate
                {
                    progressBar.Minimum = 0;
                    progressBar.Maximum = barLenght;
                    progressBar.Value = 0;
                    progressBar.Step = 1;
                });
                encryptionFunc(path);
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void encryptionFunc(string path)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    ZipFile.CreateFromDirectory(dir, dir + ".zip");
                    Directory.Delete(dir, recursive: true);
                }
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    //progressbarIncrease++;
                    //mre.WaitOne();
                    //Thread.Sleep(300);
                    encryptFile(file);
                    File.Delete(file);
                }
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    hashListOfEncrypted.Add(fileHash(file));
                }
                MessageBox.Show("Files sucessfully encrytped!");
            }
            catch (Exception exc)
            {
                throw new Exception(exc.Message);
            }
        }

        private string fileHash(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var fileStream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(fileStream);
                    string fileHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    Console.WriteLine(fileHash);
                    return fileHash;
                }
            }
        }

        private void encryptFile(string file)
        {
            byte[] fileBytes = File.ReadAllBytes(file);
            using (RijndaelManaged AES = new RijndaelManaged())
            {
                AES.Key = keyValue;
                AES.IV = IV_Value;
                byte[] encryptedFileBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, AES.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(fileBytes, 0, fileBytes.Length);
                        cs.Close();
                    }
                    encryptedFileBytes = ms.ToArray();
                }
                File.WriteAllBytes($"{file}.aes", encryptedFileBytes);
            }
        }

        private void resetParameters()
        {
            barLenght = 0;
            hashListOfEncrypted.Clear();
            hashListOfDecrypted.Clear();
        }

        private void countProgressBarLenght(string path)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                {
                    //countProgressBarLenght(dir);
                    barLenght++;
                }
                
            }
            catch (UnauthorizedAccessException exc)
            {
                throw new Exception (exc.Message);
            }
        }
    }
}
