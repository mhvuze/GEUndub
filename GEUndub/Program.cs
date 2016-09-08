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
            string debug_log = AppDomain.CurrentDomain.BaseDirectory + "\\debug.log";

            long offset_qpck_lastentry = 0;
            bool update_required = false;

            // Print header
            Console.WriteLine("GEUndub by MHVuze");
            Console.WriteLine("=========================");

            // Check files and resources
            if (!File.Exists(bin_qpck)) { Console.WriteLine("ERROR: bin.qpck doesn't exist in this directory."); Console.ReadLine(); return; }
            if (File.Exists(new_qpck)) { File.Delete(new_qpck); }
            if (!Directory.Exists(res)) { Console.WriteLine("ERROR: res folder doesn't exist in this directory."); Console.ReadLine(); return; }
            if (File.Exists(debug_log)) { File.Delete(debug_log); }

            // Processing qpck
            using (BinaryReader reader_qpck = new BinaryReader(File.Open(bin_qpck, FileMode.Open)))
            {
                if (reader_qpck.ReadInt32() != magic_qpck) { Console.WriteLine("ERROR: bin.qpck is not a valid qpck file."); Console.ReadLine(); return; }
                int count_qpck = reader_qpck.ReadInt32();

                // Copy original qpck index to new qpck
                reader_qpck.BaseStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer_qpck_index = reader_qpck.ReadBytes(8 + count_qpck * 20);  // Magic + count_qpck + entry per file
                reader_qpck.BaseStream.Seek(8, SeekOrigin.Begin);
                BinaryWriter writer_qpck = new BinaryWriter(File.OpenWrite(new_qpck));
                writer_qpck.Write(buffer_qpck_index);

                StreamWriter debuglog = new StreamWriter(debug_log, true);

                for (int i = 0; i < count_qpck; i++)
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

                    // Print progress every 5000 files
                    if ((i+1) % 5000 == 0) { Console.WriteLine("Processed {0} / {1} files.", (i+1), count_qpck); }                    

                    #region pres-processing
                    // Process .pres
                    if (file_magic == magic_pres)
                    {
                        // Check if .pres contains .is14 audio files
                        if (buffer_qpck_file.ArrayContains(pattern_is14) == true)
                        {
                            Stream stream_og_pres = new MemoryStream(buffer_qpck_file);
                            using (BinaryReader reader_pres = new BinaryReader(stream_og_pres))
                            {
                                int size_new_pres = size_qpck_file;

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
                                    reader_pres.BaseStream.Seek(offset_set_info, SeekOrigin.Begin);

                                    // Remain during set processing
                                    var set_file_dictionary = new List<SetFile>();
                                    int offset_next_file = 0;                                    

                                    for (int k = 0; k < count_set_files; k++)
                                    {
                                        IEnumerable<SetFile> foundDupe = null;

                                        // Get individual file info
                                        int offset_file = reader_pres.ReadInt32();
                                        int offset_shifted = offset_file & ((1 << (32 - 4)) - 1);

                                        long csize_file_pos = reader_pres.BaseStream.Position;
                                        int csize_file = reader_pres.ReadInt32();

                                        int offset_name = reader_pres.ReadInt32();
                                        int count_nameparts = reader_pres.ReadInt32();
                                        reader_pres.BaseStream.Seek(12, SeekOrigin.Current);

                                        long usize_file_pos = reader_pres.BaseStream.Position;
                                        int usize_file = reader_pres.ReadInt32();

                                        long reader_index_file = reader_pres.BaseStream.Position;

                                        // Get individual file name info
                                        reader_pres.BaseStream.Seek(offset_name, SeekOrigin.Begin);
                                        int offset_part_name = reader_pres.ReadInt32();
                                        int offset_part_Ext = reader_pres.ReadInt32();

                                        // Get individual file name strings
                                        string string_name = "";
                                        string string_ext = "";

                                        if (count_nameparts < 2)
                                        {
                                            debuglog.WriteLine("ERR_NAMEPARTS_LESSTWO,qpck {0},set {1},file {2}", (i + 1), (j + 1), (k + 1));
                                            break;
                                        }
                                        else
                                        {
                                            reader_pres.BaseStream.Seek(offset_part_name, SeekOrigin.Begin);
                                            string_name = readNullterminated(reader_pres);
                                            reader_pres.BaseStream.Seek(offset_part_Ext, SeekOrigin.Begin);
                                            string_ext = readNullterminated(reader_pres);
                                        }

                                        // Replace file
                                        if (string_ext != "is14") { break; }

                                        string name_new_file = res + "\\" + string_name + ".wav";
                                        if (!File.Exists(name_new_file)) { debuglog.WriteLine("ERR_FILE_MISSING,qpck {0},set {1},file {2},{3}.{4}", (i + 1), (j + 1), (k + 1), string_name, string_ext); }
                                        else
                                        {
                                            bool patchdupe = false;
                                            if (count_set_files > 1)
                                            {
                                                // Check if this is one of the stupid dupe files
                                                ILookup<string, SetFile> byName = set_file_dictionary.ToLookup(o => o.FileName);
                                                foundDupe = byName[string_name + "." + string_ext];

                                                if (foundDupe.Count() == 1)
                                                {
                                                    patchdupe = true;
                                                }
                                            }
                                            if (patchdupe == true)
                                            {
                                                var element = foundDupe.First();
                                                byte[] array_size_new_file = BitConverter.GetBytes(element.Size);
                                                byte[] array_offset_new_file = BitConverter.GetBytes(element.Offset);

                                                // Update size in pres
                                                Array.Copy(array_size_new_file, 0, buffer_qpck_file, csize_file_pos, 4);
                                                Array.Copy(array_size_new_file, 0, buffer_qpck_file, usize_file_pos, 4);
                                                Array.Copy(array_offset_new_file, 0, buffer_qpck_file, offset_set_info + (k * 0x20), 3);

                                                patchdupe = false;
                                            }
                                            else
                                            {
                                                byte[] buffer_new_file = File.ReadAllBytes(name_new_file);
                                                int size_new_file = buffer_new_file.Length;
                                                byte[] array_size_new_file = BitConverter.GetBytes(size_new_file);

                                                // Update size in pres
                                                Array.Copy(array_size_new_file, 0, buffer_qpck_file, csize_file_pos, 4);
                                                Array.Copy(array_size_new_file, 0, buffer_qpck_file, usize_file_pos, 4);

                                                // Calculate size of updated pres
                                                if (csize_file > size_new_file) { size_new_pres -= csize_file - size_new_file; }
                                                if (csize_file < size_new_file) { size_new_pres += size_new_file - csize_file; }
                                                Array.Resize(ref buffer_qpck_file, size_new_pres);

                                                if (k > 0)
                                                {
                                                    // This is dirty
                                                    byte[] temp = BitConverter.GetBytes(offset_next_file);
                                                    Array.Copy(temp, 0, buffer_qpck_file, offset_set_info + (k * 0x20), 3);
                                                    //Console.WriteLine("File: {0}, Offset: {1}", k + 1, offset_file_new);

                                                    try
                                                    {
                                                        Array.Copy(buffer_new_file, 0, buffer_qpck_file, offset_next_file, size_new_file);
                                                        set_file_dictionary.Add(new SetFile(string_name + "." + string_ext, offset_next_file, size_new_file));
                                                        offset_next_file += size_new_file;
                                                    }
                                                    catch
                                                    {
                                                        Console.WriteLine("qpck: {3}, file: {0}, offset_next_file: {1}, size_new_file: {2}", string_name, offset_next_file, size_new_file, (i + 1));
                                                        File.WriteAllBytes("debug.bin", buffer_qpck_file);
                                                        Console.ReadLine(); return;
                                                    }
                                                }
                                                else
                                                {
                                                    Array.Copy(buffer_new_file, 0, buffer_qpck_file, offset_shifted, size_new_file);
                                                    set_file_dictionary.Add(new SetFile(string_name + "." + string_ext, offset_next_file, size_new_file));
                                                    offset_next_file = offset_shifted + size_new_file;
                                                }
                                            }
                                            
                                            update_required = true;
                                            debuglog.WriteLine("MSG_FILE_REPLACED,qpck {0},set {1},file {2},{3}.wav", (i + 1), (j + 1), (k + 1), string_name);
                                        }
                                        // Prepare for next loop
                                        reader_pres.BaseStream.Seek(reader_index_file, SeekOrigin.Begin);
                                    }
                                    // Prepare for next loop
                                    if (count_pres_set > 1) { reader_pres.BaseStream.Seek(reader_index_root + ((j + 1) * 8), SeekOrigin.Begin); }
                                }
                            }
                        }
                    }
                    else { update_required = false; }
                    #endregion

                    // Write buffer to new qpck
                    writer_qpck.Write(buffer_qpck_file);

                    // Update qpck index if needed
                    if (update_required == true)
                    {
                        long offset_writer_return = writer_qpck.BaseStream.Position;

                        // Update size
                        long offset_size_update = 8 + (i * 20) + 0x10;
                        int size_new = buffer_qpck_file.Length;
                        byte[] array_size_new = BitConverter.GetBytes(size_new);
                        Array.Copy(array_size_new, 0, buffer_qpck_index, offset_size_update, 4);

                        // Update offsets
                        for (int j = i; j < count_qpck - 1; j++)
                        {
                            long offset_offset_new = 8 + ((j + 1) * 20);
                            long offset_new = 0;

                            Stream stream_qpck_index = new MemoryStream(buffer_qpck_index);
                            using (BinaryReader reader_newindex = new BinaryReader(stream_qpck_index))
                            {
                                reader_newindex.BaseStream.Seek(offset_offset_new, SeekOrigin.Begin);
                                offset_new = reader_newindex.ReadInt64();
                            }

                            int size_difference = 0;
                            if (size_new > size_qpck_file) { size_difference = size_new - size_qpck_file; offset_new += size_difference; }
                            if (size_new < size_qpck_file) { size_difference = size_qpck_file - size_new; offset_new -= size_difference; }

                            byte[] array_offset_new = BitConverter.GetBytes(offset_new);
                            Array.Copy(array_offset_new, 0, buffer_qpck_index, offset_offset_new, 8);
                        }
                        writer_qpck.BaseStream.Seek(0, SeekOrigin.Begin);
                        writer_qpck.Write(buffer_qpck_index);
                        writer_qpck.BaseStream.Seek(offset_writer_return, SeekOrigin.Begin);
                    }

                    // Prepare for next loop
                    reader_qpck.BaseStream.Seek(offset_qpck_lastentry, SeekOrigin.Begin);
                }
                writer_qpck.Close();
            }

            // Rename files
            Console.WriteLine("=========================");
            Console.WriteLine("Renaming files.");
            File.Move(bin_qpck, AppDomain.CurrentDomain.BaseDirectory + "\\bin.qpck.old");
            File.Move(new_qpck, bin_qpck);

            // App Exit
            Console.WriteLine("=========================");
            Console.WriteLine("Finished patching. Press Enter to exit.");
            Console.ReadLine();
        }

        // Read null-terminated string
        public static string readNullterminated(BinaryReader reader)
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
    class SetFile
    {
        // key properties
        public string FileName { get; private set; }
        public int Offset { get; private set; }
        public int Size { get; private set; }

        public SetFile(string filename, int offset, int size)
        {
            FileName = filename;
            Offset = offset;
            Size = size;
        }
    }

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
