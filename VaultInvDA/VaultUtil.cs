using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace VaultInventorDA
{
    class VaultUtil
    {
        private static int MAX_FILE_TRANSFER_SIZE = 49 * 1024 * 1024;   // 49 MB
        private static VDF.Vault.Currency.Connections.Connection _connection { get; set; }
        private static Autodesk.Connectivity.WebServices.File checkedoutfile { get; set; }

        /// <summary>
        /// Downloads file from Vault
        /// </summary>
        /// <returns>Array of bytes if successful</returns>
        public static byte[] DownloadFileStream(out string fileName)
        {
            fileName = string.Empty;
            try
            {
                Autodesk.Connectivity.WebServices.File file = SelectFilefromUI();
                fileName = file.Name;
               
                checkedoutfile = _connection.WebServiceManager.DocumentService.CheckoutFile
                            (file.Id, CheckoutFileOptions.Master, Environment.MachineName, string.Empty, "Checking out file", out _);

                ByteArray dwldTckt = _connection.WebServiceManager.DocumentService.GetDownloadTicketsByFileIds(new long[] { checkedoutfile.Id }).First();
                byte[] filebytes = DownloadFileResource(dwldTckt, false);
                return filebytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Select file from UI failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Check in updated file back into Vault
        /// </summary>
        /// <returns>void</returns>
        public static void CheckinFileStream(byte[] filecontents)
        {
            ByteArray uploadTckt = UploadFileResource(filecontents);
            Autodesk.Connectivity.WebServices.File checkedinFile = _connection.WebServiceManager.DocumentService.CheckinUploadedFile(checkedoutfile.MasterId,
                                                    "Updated Comments via DA", false, DateTime.Now, null, null, true, checkedoutfile.Name, 
                                                    checkedoutfile.FileClass, checkedoutfile.Hidden, uploadTckt);                                                                                              

        }

        /// <summary>
        /// Launches a Vault UI to select Ipt file
        /// </summary>
        /// <returns>Selected File</returns>
        private static Autodesk.Connectivity.WebServices.File SelectFilefromUI()
        {
            VDF.Vault.Currency.Entities.FileIteration fileIter = null;
            _connection = VDF.Vault.Forms.Library.Login(null);
            if (_connection.IsConnected)
            {
                VDF.Vault.Forms.Settings.SelectEntitySettings settings =
                  new VDF.Vault.Forms.Settings.SelectEntitySettings();

                VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter[] filters =
                    new VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter[]
                    {
                       // new VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter("Assembly Files (*.iam)", ".+iam", VDF.Vault.Currency.Entities.EntityClassIds.Files),
                        new VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter("Part Files (*.ipt)", ".+ipt", VDF.Vault.Currency.Entities.EntityClassIds.Files)
                    };

                VDF.Vault.Forms.Controls.VaultBrowserControl.Configuration initialConfig = new VDF.Vault.Forms.Controls.VaultBrowserControl.Configuration(_connection, settings.PersistenceKey, null);

                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.EntityName);
                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.CheckInDate);
                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.Comment);
                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.ThumbnailSystem);
                initialConfig.AddInitialSortCriteria(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.EntityName, true);

                settings.DialogCaption = "Select Part or Assembly file to Upload";
                settings.ActionableEntityClassIds.Add("FILE");
                settings.MultipleSelect = false;
                settings.ConfigureActionButtons("Upload", null, null, false);
                settings.ConfigureFilters("Applied filter", filters, null);
                settings.OptionsExtensibility.GetGridConfiguration = e => initialConfig;

                Console.WriteLine("Launching Vault Browser...");
                VDF.Vault.Forms.Results.SelectEntityResults results =
                    VDF.Vault.Forms.Library.SelectEntity(_connection, settings);
                if (results != null)
                {
                    fileIter = results.SelectedEntities.FirstOrDefault() as VDF.Vault.Currency.Entities.FileIteration;
                }
            }
            return fileIter;
        }

        /// <summary>
        /// Download file resource.
        /// </summary>
        /// <returns>byte array with the file's contents</returns>
        private static byte[] DownloadFileResource(ByteArray downloadTicket, bool allowSync)
        {
            _connection.WebServiceManager.FilestoreService.CompressionHeaderValue = new CompressionHeader();
            _connection.WebServiceManager.FilestoreService.CompressionHeaderValue.Supported = Compression.None;
            using (MemoryStream stream = new MemoryStream())
            {
                int bytesTransferred = 0;
                do
                {
                    using (Stream filePart = _connection.WebServiceManager.FilestoreService.DownloadFilePart(
                        downloadTicket.Bytes, (long)bytesTransferred, (long)(bytesTransferred + MAX_FILE_TRANSFER_SIZE), allowSync
                        ))
                    {
                        int partSize = (_connection.WebServiceManager.FilestoreService.FileTransferHeaderValue != null) ? _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.UncompressedSize : 0;
                        if (partSize > 0)
                        {
                            filePart.CopyTo(stream);
                            bytesTransferred += partSize;
                        }
                    }
                } while (_connection.WebServiceManager.FilestoreService.FileTransferHeaderValue != null && !_connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.IsComplete);
                byte[] fileContents = null;
                if (bytesTransferred > 0)
                {
                    fileContents = new byte[bytesTransferred];
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.Read(fileContents, 0, bytesTransferred);
                }
                return fileContents;
            }
        }


        /// <summary>
        /// Uploads file resource.
        /// </summary>
        /// <returns>Upload ticket</returns>
        private static ByteArray UploadFileResource(byte[] fileContents)
        {
             _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue = new FileTransferHeader();
             _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.Identity = Guid.NewGuid();
             _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.Extension = Path.GetExtension(checkedoutfile.Name);
             _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.Vault =  _connection.WebServiceManager.WebServiceCredentials.VaultName;
                        
            ByteArray uploadTicket = new ByteArray();
            int bytesTotal = (fileContents != null ? fileContents.Length : 0);
            int bytesTransferred = 0;
            do
            {
                int bufferSize = (bytesTotal - bytesTransferred) % MAX_FILE_TRANSFER_SIZE;
                byte[] buffer = null;
                if (bufferSize == bytesTotal)
                {
                    buffer = fileContents;
                }
                else
                {
                    buffer = new byte[bufferSize];
                    Array.Copy(fileContents, (long)bytesTransferred, buffer, 0, (long)bufferSize);
                }

                 _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.Compression = Compression.None;
                 _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.IsComplete = (bytesTransferred + bufferSize) == bytesTotal ? true : false;
                 _connection.WebServiceManager.FilestoreService.FileTransferHeaderValue.UncompressedSize = bufferSize;

                using (var fileContentsStream = new MemoryStream(fileContents))
                    uploadTicket.Bytes =  _connection.WebServiceManager.FilestoreService.UploadFilePart(fileContentsStream);
                bytesTransferred += bufferSize;

            } while (bytesTransferred < bytesTotal);

            return uploadTicket;
        }
        
    }
}
