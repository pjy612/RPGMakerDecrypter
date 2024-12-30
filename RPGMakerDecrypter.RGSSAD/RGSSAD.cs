﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGMakerDecrypter.RGSSAD.Exceptions;

namespace RPGMakerDecrypter.RGSSAD
{
    /// <summary>
    /// Represents RPG Maker RGSS Encrypted Archive.
    /// </summary>
    public class RGSSAD : IDisposable
    {
        protected readonly string FilePath;
        protected readonly BinaryReader BinaryReader;

        public List<ArchivedFile> ArchivedFiles { get; set; }

        protected RGSSAD(string filePath)
        {
            this.FilePath = filePath;
            BinaryReader = new BinaryReader(new FileStream(filePath, FileMode.Open));
        }

        /// <summary>
        /// Gets the version of RGSSAD.
        /// </summary>
        /// <param name="path">FilePath to RGSSAD archive</param>
        /// <returns></returns>
        /// <exception cref="InvalidArchiveException">
        /// Archive is in invalid format.
        /// or
        /// Header was not found for archive.
        /// </exception>
        protected int GetVersion()
        {
            string header;

            try
            {
                header = BinaryUtils.ReadCString(BinaryReader, 7);
            }
            catch (Exception)
            {
                throw new InvalidArchiveException("Archive is in invalid format.");
            }

            if (header != Constants.RGSSADHeader)
            {
                throw new InvalidArchiveException("Header was not found for archive.");
            }

            int result = BinaryReader.ReadByte();

            if (!Constants.SupportedRGSSVersions.Contains(result))
            {
                result =  -1;
            }

            BinaryReader.BaseStream.Seek(0, SeekOrigin.Begin);

            return result;
        }

        /// <summary>
        /// Extracts all files.
        /// </summary>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="overrideExisting">if set to true, overrides existing files</param>
        public void ExtractAllFiles(string outputDirectoryPath, bool overrideExisting = false)
        {
            foreach (var archivedFile in ArchivedFiles)
            {
                ExtractFile(archivedFile, outputDirectoryPath, overrideExisting);
            }
        }

        /// <summary>
        /// Extracts single file from the file.
        /// </summary>
        /// <param name="archivedFile">Archived file</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="overrideExisting">If set to true, overrides existing files</param>
        /// <param name="createDirectory">If set to true, creates directory specified in encrypted file name</param>
        /// <exception cref="System.Exception">Invalid file path. Archive could be corrupted.</exception>
        private void ExtractFile(ArchivedFile archivedFile, string outputDirectoryPath, bool overrideExisting = false, bool createDirectory = true)
        {
            var platformSpecificArchiveFilePath = ArchivedFileNameUtils.GetPlatformSpecificPath(archivedFile.Name);

            string outputPath;

            if (createDirectory)
            {
                var directoryPath = Path.GetDirectoryName(platformSpecificArchiveFilePath);

                if (directoryPath == null)
                {
                    throw new Exception("Invalid file path. Archive could be corrupted.");
                }

                if (!Directory.Exists(Path.Combine(outputDirectoryPath, directoryPath)))
                {
                    Directory.CreateDirectory(Path.Combine(outputDirectoryPath, directoryPath));
                }

                outputPath = Path.Combine(outputDirectoryPath, platformSpecificArchiveFilePath);
            }
            else
            {
                var fileName = Path.GetFileName(platformSpecificArchiveFilePath);
                outputPath = Path.Combine(outputDirectoryPath, fileName);
            }

            // Override existing file flag is set to true
            if (File.Exists(outputPath) && !overrideExisting)
            {
                return;
            }

            BinaryReader.BaseStream.Seek(archivedFile.Offset, SeekOrigin.Begin);
            var data = BinaryReader.ReadBytes(archivedFile.Size);

            var binaryWriter = new BinaryWriter(File.OpenWrite(outputPath));

            binaryWriter.Write(DecryptFileData(data, archivedFile.Key));

            binaryWriter.Close();
        }

        /// <summary>
        /// Decrypts the file from given bytes using given key.
        /// </summary>
        /// <param name="encryptedFileData">The encrypted file data.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        private byte[] DecryptFileData(byte[] encryptedFileData, uint key)
        {
            var decryptedFileData = new byte[encryptedFileData.Length];

            var tempKey = key;
            var keyBytes = BitConverter.GetBytes(key);
            var j = 0;

            for (var i = 0; i <= encryptedFileData.Length - 1; i++)
            {
                if (j == 4)
                {
                    j = 0;
                    tempKey *= 7;
                    tempKey += 3;
                    keyBytes = BitConverter.GetBytes(tempKey);
                }

                decryptedFileData[i] = (byte)(encryptedFileData[i] ^ keyBytes[j]);

                j += 1;
            }

            return decryptedFileData;
        }

        public void Dispose()
        {
            BinaryReader.Close();
            BinaryReader.Dispose();
        }

        /// <summary>
        /// Gets the RPG Maker version based on RGASSD file extension.
        /// </summary>
        /// <param name="inputPath">Path to RGSSAD file</param>
        public static RPGMakerVersion GetRPGMakerVersion(string inputPath)
        {
            if (!File.Exists(inputPath))
            {
                return RPGMakerVersion.Unknown;
            }
            
            var fi = new FileInfo(inputPath);

            if(fi.Extension.EndsWith(Constants.RpgMakerXpArchiveExtension))
            {
                return RPGMakerVersion.Xp;
            }

            if (fi.Extension.EndsWith(Constants.RpgMakerVxArchiveExtension))
            {
                return RPGMakerVersion.Vx;
            }

            if (fi.Extension.EndsWith(Constants.RpgMakerVxAceArchiveExtension))
            {
                return RPGMakerVersion.VxAce;
            }

            return RPGMakerVersion.Unknown;
        }
    }
}
