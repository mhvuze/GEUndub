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
            if (!File.Exists(bin_qpck)) { Console.WriteLine("ERROR: bin.qpck doesn't exist in this directory."); return; }
            if (File.Exists(new_qpck)) { File.Delete(new_qpck); }
            //if (!Directory.Exists(res)) { Console.WriteLine("ERROR: res folder doesn't exist in this directory."); return; }
            //if (!File.Exists(index_txt)) { Console.WriteLine("ERROR: index.txt doesn't exist in the res folder."); return; }

            // Processing qpck
            using (BinaryReader reader_qpck = new BinaryReader(File.Open(bin_qpck, FileMode.Open)))
            {
                if (reader_qpck.ReadInt32() != magic_qpck) { Console.WriteLine("ERROR: bin.qpck is not a valid qpck file."); return; }
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
                        }
                    }

                    // Write buffer to new qpck
                    writer_qpck.Write(buffer_qpck_file);

                    // End
                    reader_qpck.BaseStream.Seek(offset_qpck_lastentry, SeekOrigin.Begin);
                }

                writer_qpck.Close();
            }
            // End
            Console.WriteLine("=========================");
            Console.WriteLine("INFO: Finished patching. Press Enter to exit.");
            Console.ReadLine();
        }

        /* Search byte array for pattern
        static bool PatternFound(byte[] source, byte[] pattern)
        {
            bool found = false;
            for (int i = 0; i < source.Length; i++)
            {
                if (source.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                {
                    found = true;
                    break;
                }
            }
            return found;
        }*/
    }

    #region byte-array-searching
    // from http://stackoverflow.com/a/283648/5343630
    public static class ArraySearch
    {
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

        static readonly int[] Empty = new int[0];

        public static int[] Locate(this byte[] self, byte[] candidate)
        {
            if (IsEmptyLocate(self, candidate))
                return Empty;

            var list = new List<int>();

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                    continue;

                list.Add(i);
            }

            return list.Count == 0 ? Empty : list.ToArray();
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
