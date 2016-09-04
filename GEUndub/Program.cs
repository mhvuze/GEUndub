using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GEUndub
{
    class Program
    {
        static void Main(string[] args)
        {
            // Definitions 
            int magic_qpck = 0x37402858;
            int magic_pres = 0x73657250;
            byte[] pattern_is14 = { 0x2e, 0x69, 0x73, 0x31, 0x34 };

            // Variables
            string bin_qpck = AppDomain.CurrentDomain.BaseDirectory + "\\bin.qpck";
            string new_qpck = AppDomain.CurrentDomain.BaseDirectory + "\\bin_new.qpck";
            string res = AppDomain.CurrentDomain.BaseDirectory + "\\res";
            string index_txt = AppDomain.CurrentDomain.BaseDirectory + "\\res\\index.txt";

            long offset_qpck_lastentry = 0;

            // Print header
            Console.WriteLine("GEUndub by MHVuze");
            Console.WriteLine("=========================");

            // Check files and resources
            if (!File.Exists(bin_qpck)) { Console.WriteLine("ERROR: bin.qpck doesn't exist in this directory."); Console.ReadLine(); }
            if (File.Exists(new_qpck)) { File.Delete(new_qpck); }
            //if (!Directory.Exists(res)) { Console.WriteLine("ERROR: res folder doesn't exist in this directory."); Console.ReadLine(); }
            //if (!File.Exists(index_txt)) { Console.WriteLine("ERROR: index.txt doesn't exist in the res folder."); Console.ReadLine(); }

            // Processing qpck
            using (BinaryReader reader_qpck = new BinaryReader(File.Open(bin_qpck, FileMode.Open)))
            {
                if (reader_qpck.ReadInt32() != magic_qpck) { Console.WriteLine("ERROR: bin.qpck is not a valid qpck file."); Console.ReadLine(); }
                int count_qpck = reader_qpck.ReadInt32();

                // Copy qpck index to new qpck
                reader_qpck.BaseStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer_qpck_index = reader_qpck.ReadBytes(8 + count_qpck * 20);  // Magic + count_qpck + entry per file
                reader_qpck.BaseStream.Seek(8, SeekOrigin.Begin);
                BinaryWriter writer_qpck = new BinaryWriter(File.OpenWrite(new_qpck));
                writer_qpck.Write(buffer_qpck_index);

                for (int i = 0; i < 20; i++)
                {
                    // Get file info
                    long offset_qpck_file = reader_qpck.ReadInt64();
                    reader_qpck.BaseStream.Seek(8, SeekOrigin.Current);
                    int size_qpck_file = reader_qpck.ReadInt32();
                    offset_qpck_lastentry = reader_qpck.BaseStream.Position;

                    // Buffer file and determine type
                    reader_qpck.BaseStream.Seek(offset_qpck_file, SeekOrigin.Begin);
                    int file_magic = reader_qpck.ReadInt32();
                    reader_qpck.BaseStream.Seek(-4, SeekOrigin.Current);
                    byte[] buffer_qpck_file = reader_qpck.ReadBytes(size_qpck_file);

                    Console.WriteLine("Processing file {0}", i + 1);
                    // Process .pres
                    if (file_magic == magic_pres)
                    {
                        // Check if .pres contains .is14 audio files
                        if (buffer_qpck_file.ArrayContains(pattern_is14) == true)
                        {
                            Console.WriteLine("File @ {0} matches filter. Processing now.", offset_qpck_file.ToString("X16"));
                            Stream stream_og_pres = new MemoryStream(buffer_qpck_file);

                            using (BinaryReader reader_pres = new BinaryReader(stream_og_pres))
                            {
                                // Get general pres info
                                reader_pres.BaseStream.Seek(0x10, SeekOrigin.Begin);
                                int offset_pres_data = reader_pres.ReadInt32();
                                reader_pres.BaseStream.Seek(8, SeekOrigin.Current);
                                int count_pres_set = reader_pres.ReadInt32();
                                long reader_index_root = reader_pres.BaseStream.Position;

                                for (int j = 0; j < count_pres_set; j++)
                                {
                                    // Handle differences for pres with count > 1
                                    if (count_pres_set > 1) { int offset_pres_set_entry = reader_pres.ReadInt32(); reader_pres.BaseStream.Seek(offset_pres_set_entry, SeekOrigin.Begin); }

                                    // Read set info
                                    reader_pres.BaseStream.Seek(16, SeekOrigin.Current);
                                    int offset_set_info = reader_pres.ReadInt32();
                                    int count_set_files = reader_pres.ReadInt32();

                                    for (int k = 0; k < count_set_files; k++)
                                    {
                                        // Get individual file info
                                        reader_pres.BaseStream.Seek(offset_set_info, SeekOrigin.Begin);
                                        int offset_file = reader_pres.ReadInt32();
                                        int csize_file = reader_pres.ReadInt32();
                                        int offset_name = reader_pres.ReadInt32();
                                        int count_nameparts = reader_pres.ReadInt32();
                                        reader_pres.BaseStream.Seek(12, SeekOrigin.Current);
                                        int usize_file = reader_pres.ReadInt32();
                                        long reader_index_file = reader_pres.BaseStream.Position;

                                        // Get individual file name info
                                        reader_pres.BaseStream.Seek(offset_name, SeekOrigin.Begin);
                                        int offset_part_name = reader_pres.ReadInt32();
                                        int offset_part_Ext = reader_pres.ReadInt32();

                                        // Get individual file name strings
                                        if (count_nameparts < 2) { Console.WriteLine("ERROR: This file doesn't have 2 or more name parts."); break; }
                                        reader_pres.BaseStream.Seek(offset_part_name, SeekOrigin.Begin);
                                        string string_name = readNullterminated(reader_pres);
                                        reader_pres.BaseStream.Seek(offset_part_Ext, SeekOrigin.Begin);
                                        string string_ext = readNullterminated(reader_pres);

                                        Console.WriteLine("{0}.{1}", string_name, string_ext);

                                        // Blabla replacement

                                        // Prepare for next loop
                                        reader_pres.BaseStream.Seek(reader_index_file + (k * 0x20), SeekOrigin.Begin);
                                    }

                                    // Prepare for next loop
                                    if (count_pres_set > 1) { reader_pres.BaseStream.Seek(reader_index_root + ((j + 1) * 8), SeekOrigin.Begin); }
                                }
                            }
                        }
                    }

                    // Write buffer to new qpck
                    writer_qpck.Write(buffer_qpck_file);

                    // Prepare for next loop
                    reader_qpck.BaseStream.Seek(offset_qpck_lastentry, SeekOrigin.Begin);
                }
                writer_qpck.Close();
            }
            // App Exit
            Console.WriteLine("=========================");
            Console.WriteLine("INFO: Finished patching. Press Enter to exit.");
            Console.ReadLine();
        }

        // Read null-terminated string
        private static string readNullterminated(BinaryReader reader)
        {
            var char_array = new List<byte>();
            string str = "";
            if (reader.BaseStream.Position == reader.BaseStream.Length)
            {
                byte[] char_bytes2 = char_array.ToArray();
                str = Encoding.UTF8.GetString(char_bytes2);
                return str;
            }
            byte b = reader.ReadByte();
            while ((b != 0x00) && (reader.BaseStream.Position != reader.BaseStream.Length))
            {
                char_array.Add(b);
                b = reader.ReadByte();
            }
            byte[] char_bytes = char_array.ToArray();
            str = Encoding.UTF8.GetString(char_bytes);
            return str;
        }
    }

    #region helpers    
    public static class Helpers
    {
        // Array pattern search; based on http://stackoverflow.com/a/283648/5343630
        public static bool ArrayContains(this byte[] self, byte[] candidate)
        {
            bool contains = false;

            if (IsEmptyLocate(self, candidate))
                return contains;

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                    continue;

                contains = true;
                return contains;
            }

            return contains;
        }

        static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array[position + i] != candidate[i])
                    return false;

            return true;
        }

        static bool IsEmptyLocate(byte[] array, byte[] candidate)
        {
            return array == null
                || candidate == null
                || array.Length == 0
                || candidate.Length == 0
                || candidate.Length > array.Length;
        }
    }
    #endregion
}
