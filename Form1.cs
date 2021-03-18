using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Daugiagijis._2PD
{
    public partial class Form1 : Form
    {
        byte[] keyValue = Encoding.ASCII.GetBytes("~!@#$%^&*()_+=-0987654321`[],;'/");
        byte[] IV_Value = Encoding.ASCII.GetBytes("<>?:{}|ASDJNGHFY");
        private List<string> hashListOfEncrypted = new List<string>();
        private List<string> hashListOfDecrypted = new List<string>();
        private ManualResetEvent mre = new ManualResetEvent(true);
        private CancellationTokenSource cts;
        private string actionString = null;
        private string selectedPath;

        public Form1()
        {
            InitializeComponent();
            progressBar.Minimum = 0;
            progressBar.Value = 0;
            progressBar.Step = 1;
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
                actionString = "Encryption";
                selectedPath = PathTextBox.Text;
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                Thread th1 = new Thread(() => encryptThread(selectedPath, ct));
                th1.Name = "Encryption thread";
                th1.Start();
                //Thread th2 = new Thread(() => progressFunc(th1));
                //th2.Name = "Progress thread";
                //th2.Start();
            }
        }

        private void progressFunc()
        {
            try
            {
                Invoke((Action)delegate
                {
                    progressBar.PerformStep();
                });
                //Thread.Sleep(1000);
            }
            catch(Exception exc)
            {
                throw new Exception(exc.Message);
            }
        }

        private void encryptThread(string path, CancellationToken ct)
        {
            setParametersToWork();
            try
            {
                int barLenght = countProgressBarLenght(path);
                Invoke((Action)delegate
                {
                    progressBar.Maximum = barLenght;
                });
                encryptionFunc(path, ct);
            }
            catch (Exception exc)
            {
                resetParameters();
                MessageBox.Show(exc.Message);
            }
        }

        private void encryptionFunc(string path, CancellationToken ct)
        {
            Console.WriteLine($"{Thread.CurrentThread.Name} started.");
            try
            {
                //Archive folders
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    mre.WaitOne();
                    if(ct.IsCancellationRequested)
                    {
                        return;
                    }
                    ZipFile.CreateFromDirectory(dir, dir + ".zip");
                    Directory.Delete(dir, recursive: true);
                    Console.WriteLine($"Directory zipped: {dir}");
                }
                //Encrypt files
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    mre.WaitOne();
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    encryptFile(file);
                    Thread th2 = new Thread(() => progressFunc());
                    th2.Name = "Progress thread";
                    th2.Start();
                    th2.Join();
                }
                //Get hash values
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    mre.WaitOne();
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    string hashLine = fileHash(file);
                    hashListOfEncrypted.Add(hashLine);
                }
                //Write hash values to file
                using (StreamWriter sw = File.AppendText($"{path.GetHashCode()}.txt"))
                {
                    foreach (String hash in hashListOfEncrypted)
                    {
                        sw.WriteLine(hash);
                    }
                }
                resetParameters();
                MessageBox.Show("Files sucessfully encrytped!");
                Console.WriteLine($"{Thread.CurrentThread.Name} ended.");
            }
            catch (Exception exc)
            {
                Console.WriteLine($"{Thread.CurrentThread.Name} ended.");
                throw new Exception(exc.Message);
            }
        }

        private void EncryptionCancellationMethod(string path)
        {
            Thread th = new Thread(() =>
            {
                Console.WriteLine("Canceling...");
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(file) == ".aes")
                    {
                        decryptFile(file);
                    }
                }
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(file) == ".zip")
                    {
                        unzipFolder(file, Path.GetFileNameWithoutExtension(file), Path.GetDirectoryName(file));
                    }
                }
                Console.WriteLine("Cancelled!");
            });
            th.Name = "Cancellation thread";
            th.Start();
        }

        private void DecryptionCancellationMethod(string path)
        {
            Thread th = new Thread(() =>
            {
                Console.WriteLine("Canceling...");
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    ZipFile.CreateFromDirectory(dir, dir + ".zip");
                    Directory.Delete(dir, recursive: true);
                }
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(file) != ".aes")
                    {
                        encryptFile(file);
                    }
                }
                Console.WriteLine("Cancelled!");
            });
            th.Name = "Cancellation thread";
            th.Start();
        }

        private string fileHash(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var fileStream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(fileStream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
                File.Delete(file);
            }
            Console.WriteLine($"File encrypted: {file}");
        }

        private void setParametersToWork()
        {
            hashListOfEncrypted.Clear();
            hashListOfDecrypted.Clear();
            Invoke((Action)delegate
            {
                progressBar.Value = 0;
                PauseButton.Enabled = true;
                ContinueButton.Enabled = true;
                StopButton.Enabled = true;
            });
        }

        private void resetParameters()
        {
            Invoke((Action)delegate
            {
                PauseButton.Enabled = false;
                ContinueButton.Enabled = false;
                StopButton.Enabled = false;
            });
        }

        private int countProgressBarLenght(string path)
        {
            try
            {
                int result = 0;
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    result++;
                }
                foreach(string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    result++;
                }
                Console.WriteLine($"Bar lenght: {result}");
                return result;
            }
            catch (UnauthorizedAccessException exc)
            {
                throw new Exception(exc.Message);
            }
        }

        private void DecryptButton_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(PathTextBox.Text) || Directory.Exists(PathTextBox.Text) == false)
            {
                MessageBox.Show("Directory path is not valid!");
            }
            else
            {
                actionString = "Decryption";
                selectedPath = PathTextBox.Text;
                cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                Thread th1 = new Thread(() => decryptThread(selectedPath, ct));
                th1.Name = "Decryption thread";
                th1.Start();
                //Thread th2 = new Thread(() => progressFunc(th1));
                //th2.Name = "Progress thread";
                //th2.Start();
            }
        }

        private void decryptThread(string path, CancellationToken ct)
        {
            setParametersToWork();
            try
            {
                int barLenght = countProgressBarLenght(path);
                Invoke((Action)delegate
                {
                    progressBar.Maximum = barLenght;
                });
                getHashListOfDecrypted(path);
                decryptionFunc(path, ct);
            }
            catch (Exception exc)
            {
                resetParameters();
                MessageBox.Show(exc.Message);
            }
        }

        private void getHashListOfDecrypted(string path)
        {
            try
            {
                if (File.Exists($"{path.GetHashCode()}.txt"))
                {
                    foreach (string line in File.ReadAllLines($"{path.GetHashCode()}.txt"))
                    {
                        hashListOfDecrypted.Add(line);
                    }
                }
                else
                {
                    throw new Exception("No file with MD5 Hash values found!");
                }
            }
            catch (Exception exc)
            {
                throw new Exception(exc.Message);
            }
        }

        private void decryptionFunc(string path, CancellationToken ct)
        {
            try
            {
                Console.WriteLine($"{Thread.CurrentThread.Name} started.");
                //Decrypt files
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    mre.WaitOne();
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    string hash = fileHash(file);
                    foreach (string hashString in hashListOfDecrypted)
                    {
                        if (hash == hashString)
                        {
                            decryptFile(file);
                        }
                    }
                    Thread th2 = new Thread(() => progressFunc());
                    th2.Name = "Progress thread";
                    th2.Start();
                    th2.Join();
                }
                //Unzip folders
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    mre.WaitOne();
                    if (Path.GetExtension(file) == ".zip")
                    {
                        unzipFolder(file, Path.GetFileNameWithoutExtension(file), Path.GetDirectoryName(file));
                    }
                }
                // Delete hash file
                if (File.Exists($"{path.GetHashCode()}.txt"))
                {
                    mre.WaitOne();
                    File.Delete($"{path.GetHashCode()}.txt");
                    Console.WriteLine($"Hash file deleted!");
                }
                resetParameters();
                MessageBox.Show("Files successfully decrypted!");
                Console.WriteLine($"{Thread.CurrentThread.Name} ended.");
            }
            catch (Exception exc)
            {
                Console.WriteLine($"{Thread.CurrentThread.Name} ended.");
                throw new Exception(exc.Message);
            }
        }

        private void unzipFolder(string path, string extractName, string directory)
        {
            Console.WriteLine($"Unzipping: {path}");
            string finalDirectory = directory + "\\" + extractName;
            Directory.CreateDirectory(finalDirectory);
            ZipFile.ExtractToDirectory(path, finalDirectory);
            File.Delete(path);
        }

        private void decryptFile(string file)
        {
            byte[] content = File.ReadAllBytes(file);
            using (var AES = new RijndaelManaged())
            {
                AES.IV = IV_Value;
                AES.Key = keyValue;
                using (var memStream = new MemoryStream())
                {
                    CryptoStream cryptoStream = new CryptoStream(memStream, AES.CreateDecryptor(), CryptoStreamMode.Write);
                    cryptoStream.Write(content, 0, content.Length);
                    cryptoStream.FlushFinalBlock();
                    File.WriteAllBytes(file, memStream.ToArray());
                    string withoutExtension = Path.ChangeExtension(file, null);
                    File.Move(file, withoutExtension);
                    Console.WriteLine($"File decrypted: {file}");
                }
            }
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            mre.Reset();
        }

        private void ContinueButton_Click(object sender, EventArgs e)
        {
            mre.Set();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            mre.Reset();
            if(actionString == "Encryption")
            {
                DialogResult result = MessageBox.Show($"Do you really want to abort Encryption?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    cts.Cancel();
                    mre.Set();
                    EncryptionCancellationMethod(selectedPath);
                    setParametersToWork();
                    resetParameters();
                    MessageBox.Show("Encryption aborted!");
                }
            }
            else
            {
                DialogResult result = MessageBox.Show($"Do you really want to abort Decryption?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    cts.Cancel();
                    mre.Set();
                    DecryptionCancellationMethod(selectedPath);
                    setParametersToWork();
                    resetParameters();
                    MessageBox.Show("Decryption aborted!");
                }
            }
            mre.Set();
        }
    }
}
