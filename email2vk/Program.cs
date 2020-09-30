using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using VkNet;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Threading;
using S22.Imap;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;

namespace email2vk
{
    class Program
    {
        static string FROM_EMAIL = "*****@gmail.com";
        static string FROM_PWD = "password";
        const string SMTP_SERVER = "smtp.google.com";
        const int SMTP_PORT = 993;
        static string apptoken = "vkapp";
        static ulong application_id = 111111;
        static long group_id = 1234567;
        static long owner_id_group = -1234567;
        static string mytoken = ""; //katemobile
        static string location = System.Reflection.Assembly.GetExecutingAssembly().Location; //Место запуска exe программы
        static string attDirectory = location + "\\Attachments";
        static List<string> attachments_string = new List<string>(); //расположение вложений на диске

        private static List<MediaAttachment> attachments = new List<MediaAttachment>();

        static void Main(string[] args)
        {
            var api = new VkApi();
            api.Authorize(new ApiAuthParams
            {
                ApplicationId = application_id,
                AccessToken = apptoken,
                Settings = Settings.All
            });
            Console.WriteLine(api.Token);


            var collection = api.Groups.GetLongPollServer((ulong)group_id);
            Console.WriteLine(collection.Key + " " + collection.Server + " " + collection.Ts);


            while (true)
            {
                try
                {
                    ReadNewEmail();

                    Thread.Sleep(60000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
        }

        //Чтение почты
        public static void ReadNewEmail()
        {
            attachments_string = null; //Обнуляем

            string from = null;
            string subject = null;
            string messageText = null;
            string message2vk = null;
            System.Net.Mail.Attachment att = null;
            System.Net.Mail.AttachmentCollection att_coll = null;
            //Получение нового письма, его текста и темы, от кого (ИМЯ)
            string hostname = SMTP_SERVER,
               username = FROM_EMAIL, password = FROM_PWD;

            // The default port for IMAP over SSL is 993.
            using (ImapClient client = new ImapClient(hostname, 993, username, password, AuthMethod.Login, true))
            {
                Console.WriteLine("Связь с email-сервером установлена! Проверка почты...");


                // Returns a collection of identifiers of all mails matching the specified search criteria.
                IEnumerable<uint> uids = client.Search(SearchCondition.Unseen());

                // Download mail messages from the default mailbox.
                //ФОрмирование списка вложение из почты если они меньше 2MB
                // The expression will be evaluated for every MIME part of every mail message
                // in the uids collection.
                IEnumerable<MailMessage> messages = client.GetMessages(uids,
                    (Bodypart part) =>
                    {
                        // We're only interested in attachments.
                        if (part.Disposition.Type == ContentDispositionType.Attachment)
                        {
                            Int64 TwoMegabytes = (1024 * 1024 * 2);
                            if (part.Size > TwoMegabytes)
                            {
                                // Don't download this attachment.
                                return false;
                            }
                        }

                        // Fetch MIME part and include it in the returned MailMessage instance.
                        return true;
                    }
                );

                foreach (var mess in messages)
                {
                    //Кодировка
                    mess.BodyEncoding = System.Text.Encoding.UTF8;
                    mess.SubjectEncoding = System.Text.Encoding.UTF8;
                    mess.HeadersEncoding = System.Text.Encoding.UTF8;

                    from = mess.From.DisplayName;
                    subject = mess.Subject;
                    //var encoding = mess.HeadersEncoding;
                    messageText = mess.Body;

                    Console.WriteLine(from + "\n" + subject + "\n" + messageText);

                    message2vk = "От: " + from + "\n" + "Тема: " + subject + "\n" + "Сообщение: " + messageText;
                    if (mess.Attachments != null)
                    {
                        message2vk += "\n-------------------------\n Вложения смотрите на почте boss.rk5@mail.ru";


                    }
                }

                // Put calling thread to sleep. This is just so the example program does
                // not immediately exit.
                Thread.Sleep(100);

            }

            // Скачивание и сохранение с почты вложений

            // Отправка ВК
            if (messageText != null)
            {
 
                PosterOnWall(attachments_string, message2vk, group_id);
            }
            //return message2vk;
        }


        //Пост на стену
        public static void PosterOnWall(List<string> attachments_string_toPost, string MessageToAttach, long? GroupID)
        {
            //List<>
            VkApi api = new VkApi();
            //Авторизация
            api.Authorize(new ApiAuthParams
            {
                AccessToken = mytoken
            });

            if (attachments_string == null) //Если нет вложений
            {
                long post = api.Wall.Post(new WallPostParams
                {
                    //Attachments = atts,
                    OwnerId = -(long)GroupID,
                    Message = MessageToAttach,
                    FromGroup = true,
                });
            }
            else
            {
                int maximum_docs = 9, filecount = 0;
                // Получить адрес сервера для загрузки.
                var uploadServer = api.Docs.GetWallUploadServer(GroupID);
                // Загрузить файлы.
                var wc = new WebClient();

                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(attDirectory);
                foreach (string fileName in fileEntries)
                {
                    if (filecount <= 9)
                    {//Получить расширение файла
                        string extension = Path.GetExtension(fileName);
                        //Загрузка вложения на стену и добавление в список вложений к посту
                        attachments.AddRange(SendOnServer(fileName, extension));
  
                        filecount++;
                    }
                }
                long post = api.Wall.Post(new WallPostParams
                {
                    //Attachments = atts,
                    OwnerId = -(long)GroupID,
                    Message = MessageToAttach,
                    FromGroup = true,
                    Attachments = attachments
                });

                try
                {
                    // Очистить директорию location+"\\Attachments"
                    foreach (string fileName in fileEntries)
                    {
                        //Чистим дир.
                        File.Delete(fileName);
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }


            try //Чистим список вложений
            {
                attachments_string.Clear();
            }
            catch (Exception ex) { Console.WriteLine(ex); }

            Thread.Sleep(2000);

        }
        //Загрузка на стену и добавление во вложенные
        public static IReadOnlyCollection<Document> SendOnServer(string filename, string extension)
        {
            VkApi api = new VkApi();

            //Авторизация
            api.Authorize(new ApiAuthParams
            {
                AccessToken = mytoken
            });

            Console.WriteLine("Авторизировались");
            UploadServerInfo getWallUploadServer = api.Docs.GetWallUploadServer(group_id);
            string uploadurl = getWallUploadServer.UploadUrl;
            //long? userid = getWallUploadServer.UserId;
            //long? albumid = getWallUploadServer.AlbumId;
            string responseFile = null;
            IReadOnlyCollection<Document> doclist = null;
            // Загрузить фотографию.
            try
            {
                WebClient wc = new WebClient();
                //string responseImg = null;
               
                responseFile = Encoding.ASCII.GetString(wc.UploadFile(uploadurl, filename));
                responseFile.GetHashCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

            }
            return doclist;
        }

    }
}
