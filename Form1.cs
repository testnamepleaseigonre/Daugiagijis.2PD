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

        private int barLenght = 0;

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
                //Console.WriteLine(barLenght);
                Invoke((Action)delegate
                {
                    progressBar.Maximum = barLenght;
                });
                encryptionFunc(path);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void encryptionFunc(string path)
        {
            try
            {
                //Archive folders
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    ZipFile.CreateFromDirectory(dir, dir + ".zip");
                    Directory.Delete(dir, recursive: true);
                }
                //encrypt files
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    //progressbarIncrease++;
                    //mre.WaitOne();
                    //Thread.Sleep(300);
                    encryptFile(file);
                    File.Delete(file);
                }
                //get hash values
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    string hashLine = fileHash(file);
                    hashListOfEncrypted.Add(hashLine);
                }
                //write hash values to file
                using (StreamWriter sw = File.AppendText($"{path.GetHashCode()}.txt"))
                {
                    foreach (String hash in hashListOfEncrypted)
                    {
                        sw.WriteLine(hash);
                    }
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
                    //Console.WriteLine(fileHash);
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
            }
        }

        private void resetParameters()
        {
            barLenght = 0;
            progressBar.Value = 0;
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
                string path = PathTextBox.Text;
                Thread th1 = new Thread(() => decryptThread(path));
                th1.Name = "Decryption thread";
                th1.Start();
            }
        }

        private void decryptThread(string path)
        {
            resetParameters();
            try
            {
                countProgressBarLenght(path);
                Console.WriteLine(barLenght);
                Invoke((Action)delegate
                {
                    progressBar.Maximum = barLenght;
                });
                getHashListOfDecrypted(path);
                decryptionFunc(path);
            }
            catch (Exception exc)
            {
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
                    throw new Exception("No MD5 Hash values found!");
                }
            }
            catch (Exception exc)
            {
                throw new Exception(exc.Message);
            }
        }
        private void decryptionFunc(string path)
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    string hash = fileHash(file);
                    foreach (string hashString in hashListOfDecrypted)
                    {
                        if (hash == hashString)
                        {
                            decryptFile(file);
                        }
                    }
                }
                //unzip folders
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(file) == ".zip")
                    {
                        unzipFolder(file, Path.GetFileNameWithoutExtension(file), Path.GetDirectoryName(file));
                    }
                }
                // delete hash file
                if (File.Exists($"{path.GetHashCode()}.txt"))
                {
                    File.Delete($"{path.GetHashCode()}.txt");
                    Console.WriteLine($"Hash file deleted!");
                }
                MessageBox.Show("Files successfully decrypted!");
            }
            catch (Exception exc)
            {
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

                    Console.WriteLine($"Decrypted success: {file}");
                }
            }
        }
    }
}
