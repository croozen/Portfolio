using Activity.Services.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Activity.Services.xTest.Helper;
using Xunit;

namespace Activity.Services.xUnit.IntegrationTests
{
    [Collection("WebTest")]
    public class IntTestActivityController : IClassFixture<TestFixture<Activity.Services.webapi.Startup>>
    {
        private readonly HttpClient _client;
        private static readonly string JSON_GETITEMFILTERWRAP = "{\"itemstatus\": {0},\"filters\":[{1}]}";
        private static readonly string JSON_GEOLOCATION = "{\"latitude\": {0},\"longitude\":{1}}";
        private static readonly string JSON_GETITEMFILTER = "{\"statement\":{\"field\": \"{0}\",\"condition\":\"{1}\",\"value\":\"{2}\"}}";
        private static readonly string JSON_GETITEMFILTERJOIN = "{\"statement\":{\"field\": \"{0}\",\"condition\":\"{1}\",\"value\":\"{2}\"}, \"join\": \"{3}\"}";
        private static readonly string JSON_GETITEMINDEXED = "{\"itemstate\": 3,\"filterindex\":{\"startdate\":\"{0}\",\"hours\":\"{1}\",\"locationid\":\"\",\"showoverdue\":\"\",\"showmyactivity\":\"\"}}";
        private static readonly string DATETIME_APPFORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        private static readonly string JWT_USER = "username";
        private static readonly string JWT_PASSWORD = "password";   

        public IntTestActivityController(TestFixture<Activity.Services.webapi.Startup> fixture)
        {
            _client = fixture.Client;
        }

        [Fact]
        public void VerifyTest()
        {
            Assert.True(1 == 1);
        }

        [Theory, InlineData(@"1234")]
        public void GetTaskByDevice(string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);

            string message = GetTasks(jwtBearerToken);

            Assert.True(message.Length > 0);
        }

        [Theory, InlineData(@"Task Test Location Rights", "guid", "High","role.1", "11@cloudci.com", "44@002.cloudci.com", JWT_USER)]
        public void GetTaskByDeviceWithLocationFilter(string title, string description, string priority, string assignedTo, string successUser1, string failUser1, string adminUser1)
        {
            string jwtBearerToken = TokenHelper.GetBearer(successUser1, JWT_PASSWORD, "1234", this._client);
            string insertMessage = GetFields(1, jwtBearerToken);
            Guid guidForValidation = Guid.NewGuid();
            List<string> fields = new List<string>();
            List<string> values = new List<string>();
            fields.Add("title");fields.Add("priority"); fields.Add("assigned to");fields.Add("description");fields.Add("start date");
            values.Add(title); values.Add(priority); values.Add(assignedTo); values.Add(description + guidForValidation.ToString());values.Add(DateTime.UtcNow.AddMinutes(-2).ToString(DATETIME_APPFORMAT));
            JObject activityItem = JObject.Parse(insertMessage);
            activityItem = CreateMessageForInsertActivity(activityItem, fields, values);

            HttpResponseMessage result = InsertTask(JsonConvert.SerializeObject(activityItem), jwtBearerToken);
            Assert.True(result.IsSuccessStatusCode);
            
            
            //Non-admin user with correct assigned location can retrieve task(s)
            string message = GetTasks(jwtBearerToken);
            Assert.Contains(guidForValidation.ToString(), message);
            
            //Non-admin user with incorrect assigned location won't be able to retrieve tasks(s)
            string jwtBearerTokenOtherNonAdmin = TokenHelper.GetBearer(failUser1, JWT_PASSWORD, "1234", this._client);
            string messageOtherNonAdmin = GetTasks(jwtBearerTokenOtherNonAdmin);
            Assert.DoesNotContain(guidForValidation.ToString(), messageOtherNonAdmin);

            //System admin can retrieve tasks(s) without assigned location filter
            string jwtBearerTokenAdmin = TokenHelper.GetBearer(adminUser1, JWT_PASSWORD, "1234", this._client);
            string messageAdmin = GetTasks(jwtBearerTokenAdmin);
            Assert.Contains(guidForValidation.ToString(), messageAdmin);
        }


        [Theory]
        [InlineData(@"1234", 86400000)]
        [InlineData(@"1234", 1)]
        public void GetTasksByDate(string deviceId, int secondsback)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            DateTime checkUpdate = DateTime.UtcNow.AddSeconds(-(secondsback));
            string message = GetTasks(jwtBearerToken, checkUpdate);

            Assert.True(message.Length > 0);
        }

        [Theory, InlineData("1234")]
        public void InsertTasks(string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            string insertMessage = GetFields(1, jwtBearerToken);
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage);

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(modInsertMessage);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "activityJson"
                };
                content.Add(jsonItem);

                // Make a call to Web API
                var result = this._client.PostAsync("api/activity", content).Result;
                Assert.True(result.IsSuccessStatusCode);
            }
        }

        [Theory]
        [InlineData("1234", 3, "PropertyBag", "Alt", "Call Help Jane", "Customer Request", "Medium", "[ME]")]
        [InlineData("1234", 3, "PropertyBag", "Alt", "Threshold Exceeded AVD", "Something Somthing", "Low", "role.1")]
        [InlineData("1234", 3, "PropertyBag", "Alt", "VIP Trevor calling", "Wish to cancel services 1mil account", "High", "role.1")]
        public void InsertFilterForTasks(string deviceId, int activityType, string fieldname, string fieldvalue, string title, string description, string priority, string assignedTo)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            string insertMessage = GetFields(activityType, jwtBearerToken);
            fieldvalue = fieldvalue + Guid.NewGuid().ToString();
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage, title, description, priority, fieldname, fieldvalue, assignedTo);

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(modInsertMessage);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "activityJson"
                };
                content.Add(jsonItem);

                // Make a call to Web API
                var result = this._client.PostAsync("api/activity", content).Result;
                Assert.True(result.IsSuccessStatusCode);
            }
            List<FilterAttributes> filters = new List<FilterAttributes>();
            filters.Add(new FilterAttributes(fieldname, "=", fieldvalue));
            string jsonFilter = CreateJsonFilter(filters);

            string message = GetTasksFilter(jwtBearerToken, jsonFilter);

            Assert.Contains(fieldvalue, message);
        }

        [Theory]
        [InlineData("web", 3, "PropertyBag", "Alt", "VIP Trevor calling", "Wish to cancel services 1mil account!", "High", "role.1")]
        public void InsertFilterTodayForTasks(string deviceId, int activityType, string fieldname, string fieldvalue, string title, string description, string priority, string assignedTo)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            string insertMessage = GetFields(activityType, jwtBearerToken);
            fieldvalue = fieldvalue + Guid.NewGuid().ToString();
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage, title, description, priority, fieldname, fieldvalue, assignedTo);

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(modInsertMessage);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "activityJson"
                };
                content.Add(jsonItem);

                // Make a call to Web API
                var result = this._client.PostAsync("api/activity", content).Result;
                Assert.True(result.IsSuccessStatusCode);
            }

            //List<FilterAttributes> filters = new List<FilterAttributes>();
            //string[] filterfield = filterfields.Split(',');
            //string[] filterop = filterOps.Split(',');
            //string[] filtervalue = filterValues.Split(',');
            //string[] filterjoin = filterJoins.Split(',');

            //for (int i = 0; i < filterfield.Length; i++)
            //{
            //    filters.Add(new FilterAttributes(filterfield[i], filterop[i], parseTestValue(filtervalue[i]), filterjoin[i]));
            //}
            string jsonFilter = JSON_GETITEMINDEXED.Replace("{0}", DateTime.UtcNow.AddHours(-2).ToString(DATETIME_APPFORMAT));
            jsonFilter = jsonFilter.Replace("{1}", "24");

            string message = GetTasksFilter(jwtBearerToken, jsonFilter);

            Assert.Contains(fieldvalue, message);
        }

        private string parseTestValue(string value)
        {
            string returnValue = value;

            if (value.Contains("[TODAY]"))
            {
                DateTime today = DateTime.UtcNow;
                string[] valuegroupsub = value.Split('-');
                string[] valuegroupplus = value.Split('+');

                if (valuegroupsub.Length >= 2)
                    today = today.AddDays(-double.Parse(valuegroupsub[1]));

                if (valuegroupplus.Length >= 2)
                    today = today.AddDays(double.Parse(valuegroupplus[1]));
                returnValue = today.ToString(DATETIME_APPFORMAT);
            }

            return returnValue;
        }

        [Theory]
        [InlineData(@"Activity.Services.UnitTest\Activity.Services.xUnit\TestFiles\SKU337887204079.pcm", "337887204079", "1234")]
        [InlineData(@"Activity.Services.UnitTest\Activity.Services.xUnit\TestFiles\CustomerHelpAisle4.pcm", "Customer", "1234")]
        [InlineData(@"Activity.Services.UnitTest\Activity.Services.xUnit\TestFiles\StockBananas.pcm", "Stock", "1234")]
        public void VoiceTasks(string audiofile, string expectedResult, string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);

            string audioFilePath = FileHelper.getFileLocation(audiofile);
            //var response = await _client.GetAsync("/api/values/");
            this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);
            var content = new MultipartFormDataContent();

            // Add login audio file content 
            var fileContent = new ByteArrayContent(File.ReadAllBytes(audioFilePath));

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/x-wav");

            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                FileName = Path.GetFileName(audioFilePath),
                Name = "activityaudio"
            };
            content.Add(fileContent);

            // Add locale
            var fileLocale = new StringContent(System.Globalization.CultureInfo.CurrentCulture.Name);
            fileLocale.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "locale"
            };
            content.Add(fileLocale);

            // Make a call to the Users service
            var response = this._client.PostAsync("/api/activity/voice", content).Result;
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                //int count = 0;
                //A response of 202 Accepted means User identification is in-progress.
                //The 202 response will contain a location header that tells us where to check status
                //and a retry-after header that tells us how long to wait before checking.
                while (response.StatusCode == HttpStatusCode.Accepted)
                {
                    Uri locationUri = response.Headers.Location;

                    // Sleep for the specified amount of time.
                    if (response.Headers.RetryAfter.Delta.HasValue)
                    {
                        Thread.Sleep(response.Headers.RetryAfter.Delta.Value);

                        // Call the endpoint specified in the location header.
                        response = this._client.GetAsync(locationUri.AbsoluteUri).Result;
                    }
                    //count++;
                }

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string tokenResponse = response.Content.ReadAsStringAsync().Result;
                    JObject json = JObject.Parse(tokenResponse);
                    //var json = JObject.Parse(tokenResponse);
                }
            }
        }

        [Theory, InlineData("1234")]
        public void UpdateTasks(string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            string insertMessage = GetFields(1, jwtBearerToken);
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage);
            HttpResponseMessage resultIns = InsertTask(modInsertMessage, jwtBearerToken);
            string itemId = resultIns.Content.ReadAsStringAsync().Result;
            itemId = itemId.Replace("\"", string.Empty);

            string activityMessage = GetTasks(itemId, jwtBearerToken);
            JObject activityItem = JObject.Parse(activityMessage);
            string modUpdateMessage = UpdateMessageForInsertActivity(activityItem);
            string activityId = (string)activityItem["id"];

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(modUpdateMessage);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "activityJson"
                };
                content.Add(jsonItem);

                // Make a call to Web API
                var result = this._client.PutAsync(string.Format("api/activity/{0}", activityId), content).Result;
                Assert.True(result.IsSuccessStatusCode);
            }
        }

        [Theory] 
            [InlineData(@"1234", "Status,Priority,Assigned To", "In Progress,High,role.2")]
            [InlineData(@"1234", "Status,Priority, Title", "In Progress,High, A change of Heart")]
        public void UpdateTasksWithMinimumFields(string deviceId, string fields, string fieldsvalue)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            string[] fieldsarray = fields.Split(',');
            string[] fieldsvaluearray = fieldsvalue.Split(',');

            // Create a task
            string insertMessage = GetFields(1, jwtBearerToken);
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage);
            HttpResponseMessage result = InsertTask(modInsertMessage, jwtBearerToken);
            string itemId = result.Content.ReadAsStringAsync().Result;
            itemId = itemId.Replace("\"", string.Empty);

            // Now get the updated Task by lookup
            string itemjson = GetTasks(itemId, jwtBearerToken);

            // Parse the item
            JObject activityItem = JObject.Parse(itemjson);
            string modUpdateMessage = UpdateMessageWithFieldNameAndValueOnly(activityItem, fieldsarray, fieldsvaluearray);

            HttpResponseMessage result2 = UpdateTask(modUpdateMessage, itemId, jwtBearerToken);

            Assert.True(result2.IsSuccessStatusCode);
        }

        [Theory, InlineData(@"1234", "Test Recur: ", "Description, Status, Priority, Assigned To", "Random Description Blah, In Progress, High, role.1", 5, 40, "{\"timezoneOffset\":\"-8:00\",\"startDate\":\"2017-11-18\",\"recurrence\":{\"frequency\":\"week\",\"interval\":1,\"schedule\":{\"weekDays\":[\"mo\",\"tu\"],\"startTime\":\"09:05\",\"duration\":\"12:00\"},\"count\":10,\"endDate\":\"2017-11-29\"}}")]
        public void CreateRecurringTask(string deviceId, string title, string fields, string fieldsvalue, int startDay, int dayDuration, string recurJson)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);

            List<string> fieldsarray = new List<string>(fields.Split(','));
            List<string> fieldsvaluearray = new List<string>(fieldsvalue.Split(','));

            DateTime now = DateTime.Now;
            ////// Commented it out to avoid double conversion to UTC
            //TimeSpan zoneoffset = TimeZoneInfo.Local.GetUtcOffset(now);
            //recurJson = recurJson.Replace("{0}", string.Format("{0}:{1:00}", (int)zoneoffset.Hours, zoneoffset.Minutes));
            //DateTime startDate = now.AddDays(startDay);
            //recurJson = recurJson.Replace("{1}", startDate.ToString("MM/dd/yyyy"));
            //DateTime endDate = startDate.AddDays(dayDuration);
            //recurJson = recurJson.Replace("{2}", endDate.ToString("MM/dd/yyyy"));

            //Add Title
            fieldsarray.Add("Title");
            fieldsvaluearray.Add(title + now.Ticks.ToString());

            //Add Recurrence
            fieldsarray.Add("Recurrence");
            fieldsvaluearray.Add(recurJson);

            // Create a task
            string insertMessage = GetFields(1, jwtBearerToken);
            //string modInsertMessage = CreateMessageForInsertActivity(insertMessage);

            // Parse the item
            JObject activityItem = JObject.Parse(insertMessage);
            activityItem = CreateMessageForInsertActivity(activityItem, fieldsarray, fieldsvaluearray);

            HttpResponseMessage result2 = InsertTask(JsonConvert.SerializeObject(activityItem), jwtBearerToken);

            Assert.True(result2.IsSuccessStatusCode);

            // Do a filter for the parentId
            string parentItemId = result2.Content.ReadAsStringAsync().Result;
            parentItemId = parentItemId.Replace("\"", string.Empty);
            List<FilterAttributes> filters = new List<FilterAttributes>();
            filters.Add(new FilterAttributes("Parent", "=", parentItemId));
            string jsonFilter = CreateJsonFilter(filters);

            string message = GetTasksFilter(jwtBearerToken, jsonFilter);
            JArray virtualActivities = JArray.Parse(message);
            JObject virtualitem = (JObject)virtualActivities[0];

            string changeToVirtualItem = UpdateMessageWithFieldNameAndValueOnly(virtualitem, new string[] { "Title" }, new string[] { title + "Unvirt" });
            string activityId = (string)virtualitem["id"];
            HttpResponseMessage result3 = UpdateTask(changeToVirtualItem, activityId, jwtBearerToken);

            Assert.True(result3.IsSuccessStatusCode);

            //Validate if recurring task start and end date time got converted again to UTC
            // Make a call to Web API
            var result = this._client.GetAsync(string.Format("api/activity/{0}", parentItemId)).Result;
            Assert.Contains("startTime\\\":\\\"09:05", result.Content.ReadAsStringAsync().Result);
        }


        [Theory, InlineData(@"1234", "Test Recur: ", "Description, Status, Priority, Assigned To", "Random Description Blah, In Progress, High, role.1",  "{\"timezoneOffset\":\"-8:00\",\"startDate\":\"{0}\",\"recurrence\":{\"frequency\":\"week\",\"interval\":1,\"schedule\":{\"weekDays\":[\"mo\",\"tu\"],\"startTime\":\"09:05\",\"duration\":\"12:00\"},\"count\":10,\"endDate\":\"{1}\"}}")]
        public void DeleteRecurringTask(string deviceId, string title, string fields, string fieldsvalue, string recurJson)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);

            List<string> fieldsarray = new List<string>(fields.Split(','));
            List<string> fieldsvaluearray = new List<string>(fieldsvalue.Split(','));

            DateTime now = DateTime.Now;

            //Add Title
            fieldsarray.Add("Title");
            fieldsvaluearray.Add(title + now.Ticks.ToString());

            recurJson = recurJson.Replace("{0}", DateTime.UtcNow.AddDays(-10).ToShortDateString());
            recurJson = recurJson.Replace("{1}", DateTime.UtcNow.AddDays(10).ToShortDateString());

            //Add Recurrence
            fieldsarray.Add("Recurrence");
            fieldsvaluearray.Add(recurJson);

            // Create a task
            string insertMessage = GetFields(1, jwtBearerToken);
            //string modInsertMessage = CreateMessageForInsertActivity(insertMessage);

            // Parse the item
            JObject activityItem = JObject.Parse(insertMessage);
            activityItem = CreateMessageForInsertActivity(activityItem, fieldsarray, fieldsvaluearray);

            HttpResponseMessage result2 = InsertTask(JsonConvert.SerializeObject(activityItem), jwtBearerToken);

            Assert.True(result2.IsSuccessStatusCode);

            // Do a filter for the parentId
            string parentItemId = result2.Content.ReadAsStringAsync().Result;
            parentItemId = parentItemId.Replace("\"", string.Empty);

            // Make a call to Web API
            var result = this._client.DeleteAsync(string.Format("api/activity/{0}", parentItemId.ToString())).Result;
            Assert.True(result.IsSuccessStatusCode);
        }

        [Theory, InlineData(@"1234")]
        public void UpdateTaskStatus(string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);

            string insertMessage = GetFields(1, jwtBearerToken);
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage);
            HttpResponseMessage resultIns = InsertTask(modInsertMessage, jwtBearerToken);
            string itemId = resultIns.Content.ReadAsStringAsync().Result;
            itemId = itemId.Replace("\"", string.Empty);

            string activityMessage = GetTasks(itemId, jwtBearerToken);
            JObject activityItem = JObject.Parse(activityMessage);
            string activityId = (string)activityItem["id"];

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);
                                
                // Add StatusId
                var statusid = new StringContent(((int)EntityStatus.Staged).ToString());
                statusid.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "stateid"
                };
                content.Add(statusid);

                // Make a call to Web API
                var result = this._client.PutAsync(string.Format("api/activity/{0}/state", activityId.ToString()), content).Result;
                Assert.True(result.IsSuccessStatusCode);
            }
        }

        [Theory, InlineData(@"1234")]
        public void DeleteTask(string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            Guid activityId = CreateStandardInsert(jwtBearerToken);

            this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

            // Make a call to Web API
            var result = this._client.DeleteAsync(string.Format("api/activity/{0}", activityId.ToString())).Result;
            Assert.True(result.IsSuccessStatusCode);
        }

        [Theory, InlineData(@"1234")]
        public void GetRequiredField(string deviceId)
        {
            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);
            string message = GetFields(1, jwtBearerToken);

            // Test for a field that is required to be in any Activity
            Assert.Contains("Modified Date", message);
        }

        private string GetTasks(string jwtBearerToken)
        {
            string message;
            using (var content = new StringContent(string.Empty, Encoding.UTF8, "application/json"))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                // Make a call to Web API
                var result = this._client.GetAsync("api/activity").Result;

                Assert.True(result.IsSuccessStatusCode);

                message = result.Content.ReadAsStringAsync().Result;
            }
            return message;
        }

        private string GetTasks(string taskId, string jwtBearerToken)
        {
            string message;
            using (var content = new StringContent(string.Empty, Encoding.UTF8, "application/json"))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                // Make a call to Web API
                var result = this._client.GetAsync("api/activity/" + taskId).Result;

                Assert.True(result.IsSuccessStatusCode);

                message = result.Content.ReadAsStringAsync().Result;
            }
            return message;
        }

        private string GetTasks(string jwtBearerToken, DateTime lastupdate)
        {
            string message;
            using (var content = new StringContent(string.Empty, Encoding.UTF8, "application/json"))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                // Make a call to Web API
                string url = "api/activity/?lastupdate=" + lastupdate.ToString("s", CultureInfo.InvariantCulture);
                var result = this._client.GetAsync(url).Result;

                Assert.True(result.IsSuccessStatusCode);

                message = result.Content.ReadAsStringAsync().Result;
            }
            return message;
        }

        private string GetTasksFilter(string jwtBearerToken, string jsonFilter)
        {
            string message;

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(jsonFilter);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "jsonFilter"
                };
                content.Add(jsonItem);

                // Make a call to Web API
                var result = this._client.PostAsync(string.Format("api/activity/filter"), content).Result;
                Assert.True(result.IsSuccessStatusCode);

                message = result.Content.ReadAsStringAsync().Result;
            }
            
            return message;
        }

        private HttpResponseMessage InsertTask(string itemjson, string jwtBearerToken)
        {
            HttpResponseMessage result = null;
            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(itemjson);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "activityJson"
                };
                content.Add(jsonItem);
                
                content.Headers.Add("geolocation", "46.876544,-78.456372");
                //buffer = System.Text.Encoding.UTF8.GetBytes(geolocation);
                //var geoloc = new ByteArrayContent(buffer);
                //geoloc.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                //geoloc.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                //{
                //    Name = "geolocation"
                //};
                //content.Add(jsonItem);

                // Make a call to Web API
                result = this._client.PostAsync("api/activity", content).Result;
            }

            return result;
        }

        private HttpResponseMessage UpdateTask(string itemjson, string itemId, string jwtBearerToken)
        {
            HttpResponseMessage result = null;
            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var buffer = System.Text.Encoding.UTF8.GetBytes(itemjson);
                var jsonItem = new ByteArrayContent(buffer);
                jsonItem.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "activityJson"
                };
                content.Add(jsonItem);

                // Make a call to Web API
                result = this._client.PutAsync(String.Format("api/activity/{0}", itemId), content).Result;
            }

            return result;
        }

        private string CreateJsonFilter(List<FilterAttributes> attributes)
        {
            string jsonFull = "";
            string jsonFullFilter = "";
            for (int i = 0; i < attributes.Count; i++)
            {
                string filter;
                if (attributes[i].Fieldjoin != null && attributes[i].Fieldjoin.Trim().Length > 0)
                {
                    filter = JSON_GETITEMFILTERJOIN.Replace("{0}", attributes[i].Field);
                    filter = filter.Replace("{1}", attributes[i].Fieldoperator);
                    filter = filter.Replace("{2}", attributes[i].Fieldvalue);
                    filter = filter.Replace("{3}", attributes[i].Fieldjoin);
                }
                else
                {
                    filter = JSON_GETITEMFILTER.Replace("{0}", attributes[i].Field);
                    filter = filter.Replace("{1}", attributes[i].Fieldoperator);
                    filter = filter.Replace("{2}", attributes[i].Fieldvalue);
                }

                if (jsonFullFilter.Length > 0)
                    jsonFullFilter += ",";
                jsonFullFilter += filter;
            }

            jsonFull = JSON_GETITEMFILTERWRAP.Replace("{0}", "3");
            jsonFull = jsonFull.Replace("{1}", jsonFullFilter);

            return jsonFull;
        }

        private class FilterAttributes
        {
            public string Field { get; set; }
            public string Fieldoperator { get; set; }
            public string Fieldvalue { get; set; }
            public string Fieldjoin { get; set; }

            public FilterAttributes(string field, string fieldoperator, string fieldvalue)
            {
                this.Field = field;
                this.Fieldoperator = fieldoperator;
                this.Fieldvalue = fieldvalue;
            }

            public FilterAttributes(string field, string fieldoperator, string fieldvalue, string fieldjoin)
            {
                this.Field = field;
                this.Fieldoperator = fieldoperator;
                this.Fieldvalue = fieldvalue;
                this.Fieldjoin = fieldjoin;
            }
        }

        private string GetFields(int activitytypeId, string jwtBearerToken)
        {
            string message;
            
            using (var content = new StringContent(string.Empty, Encoding.UTF8, "application/json"))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                // Make a call to Web API
                var result = this._client.GetAsync("api/activity/Fields/" + activitytypeId).Result;

                Assert.True(result.IsSuccessStatusCode);

                message = result.Content.ReadAsStringAsync().Result;
            }
            return message;
        }


        [Theory]
        [InlineData("Case1", "High", "role.1", 
            "Case2", "low", "role.1",
            @"1234", "Title,Priority,Finance,Bubble Color",4 , 
            "Title", "=", "Case1")]
        public void PostJsonFilterWithOptions(string title1, string priority1, string assignedTo1, 
            string title2, string priority2, string assignedTo2,
            string deviceId, string filterColumn, int columNum, 
            string fieldname1, string operand1, string fieldvalue1)
        {
            //Building JSON filter
            List<FilterAttributes> filters = new List<FilterAttributes>();
            filters.Add(new FilterAttributes(fieldname1, operand1, fieldvalue1));
            
            string jsonFilter = CreateJsonFilter(filters);

            string jwtBearerToken = TokenHelper.GetBearer(JWT_USER, JWT_PASSWORD, deviceId, this._client);

            UnitTestActivities.InsertItem(1, 1, Guid.Empty.ToString(), title1, priority1, assignedTo1);
            UnitTestActivities.InsertItem(1, 1, Guid.Empty.ToString(), title1, priority2, assignedTo2);

            using (var content = new MultipartFormDataContent())
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtBearerToken);

                var jsonItem = new StringContent(jsonFilter);
                jsonItem.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "jsonFilter"
                };
                content.Add(jsonItem);

                var filterOptions = new StringContent(filterColumn);
                filterOptions.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "filterOptions"
                };
                content.Add(filterOptions);

                var returns = this._client.PostAsync(string.Format("api/activity/FilterWithOptions"), content).Result;
                JObject activityFilterField = JObject.Parse(returns.Content.ReadAsStringAsync().Result);
                var dic = activityFilterField["avaliableFields"];
                SortedDictionary<string, List<string>> avaliableFields = dic.ToObject<SortedDictionary<string,List<string>>>();


                Assert.Contains(title1, avaliableFields["Title"]);
                Assert.True(avaliableFields["Title"].Count == 1);
                Assert.Contains(priority1, avaliableFields["Priority"]);
                Assert.Contains(priority2, avaliableFields["Priority"]);
                Assert.True(avaliableFields["Priority"].Count == 2);
                Assert.True(avaliableFields.Count == columNum);
            }
        }

        private string CreateMessageForInsertActivity(string fieldMessage)
        {
            JObject jobj = JObject.Parse(fieldMessage);
            JArray fields = (JArray)jobj["fields"]["fieldList"];
            for (int i = 0; i < fields.Count; i++)
            {
                bool isReadOnly = (bool)fields[i]["isReadOnly"];

                if (!isReadOnly)
                {
                    string fieldName = (string)fields[i]["name"];

                    if (fieldName.ToLower() == "title")
                        fields[i]["value"] = "Task 123 Assignment out: " + DateTime.Now.ToString("yyyyMMdd") ;
                    else if (fieldName.ToLower() == "priority")
                        fields[i]["value"] = "High";
                    else if (fieldName.ToLower() == "assigned to")
                        fields[i]["value"] = "role.1";
                }
            }

            return JsonConvert.SerializeObject(jobj);
        }

        private string CreateMessageForInsertActivity(string fieldMessage, string title, string description, string priority, string fieldname, string fieldvalue, string assignedTo)
        {
            JObject jobj = JObject.Parse(fieldMessage);
            JArray fields = (JArray)jobj["fields"]["fieldList"];
            for (int i = 0; i < fields.Count; i++)
            {
                bool isReadOnly = (bool)fields[i]["isReadOnly"];

                if (!isReadOnly)
                {
                    string fieldName = (string)fields[i]["name"];

                    if (fieldName.ToLower() == "title")
                        fields[i]["value"] = title;
                    if (fieldName.ToLower() == "description")
                        fields[i]["value"] = description;
                    else if (fieldName.ToLower() == "priority")
                        fields[i]["value"] = priority;
                    else if (fieldName.ToLower() == "assigned to")
                        fields[i]["value"] = assignedTo;
                    else if (fieldName.ToLower() == fieldname.ToLower())
                        fields[i]["value"] = fieldvalue;
                    else if (fieldName.ToLower() == "start date")
                        fields[i]["value"] = DateTime.UtcNow.ToString(DATETIME_APPFORMAT);
                }
            }

            return JsonConvert.SerializeObject(jobj);
        }

        private Guid CreateStandardInsert(string jwtBearerToken)
        {
            string insertMessage = GetFields(1, jwtBearerToken);
            string modInsertMessage = CreateMessageForInsertActivity(insertMessage);

            HttpResponseMessage result = InsertTask(modInsertMessage, jwtBearerToken);
            string itemId = result.Content.ReadAsStringAsync().Result;
            itemId = itemId.Replace("\"", string.Empty);

            return new Guid(itemId);
        }

        private JObject CreateMessageForInsertActivity(JObject jsonFields, List<string> fieldNames, List<string> fieldValues)
        {
            JArray fields = (JArray)jsonFields["fields"]["fieldList"];
            for (int j = 0; j < fieldNames.Count; j++)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    bool isReadOnly = (bool)fields[i]["isReadOnly"];

                    if (!isReadOnly)
                    {
                        string fieldName = (string)fields[i]["name"];

                        if (fieldName.ToLower() == fieldNames[j].ToLower())
                            fields[i]["value"] = fieldValues[j];
                    }
                }
            }

            return jsonFields; //JsonConvert.SerializeObject(jobj);
        }

        private string UpdateMessageForInsertActivity(JObject jObjectToUpdate)
        {
            JArray fields = (JArray)jObjectToUpdate["fields"]["fieldList"];
            for (int i = 0; i < fields.Count; i++)
            {
                bool isReadOnly = (bool)fields[i]["isReadOnly"];

                if (!isReadOnly)
                {
                    string fieldName = (string)fields[i]["name"];

                    if (fieldName.ToLower() == "title")
                        fields[i]["value"] = "Upd: Task Assignment out: " + DateTime.Now.ToString("yyyyMMdd");
                    else if (fieldName.ToLower() == "priority")
                        fields[i]["value"] = "Medium";
                    else if (fieldName.ToLower() == "assigned to")
                        fields[i]["value"] = "role.1";
                }
            }

            return JsonConvert.SerializeObject(jObjectToUpdate);
        }

        private string UpdateMessageWithFieldNameAndValueOnly(JObject jItem, string[] fields, string[] fieldsvalue)
        {
            JObject jUpdated = (JObject)jItem.DeepClone();
            JArray jfields = (JArray)jItem["fields"]["fieldList"];
            JArray jUpdatedfields = (JArray)jUpdated["fields"]["fieldList"];
            for (int i = jfields.Count-1; i >= 0; i--)
            {
                string jfieldName = (string)jfields[i]["name"];

                bool isFound = false;
                for (int j=0; j < fields.Length; j++)
                {
                    string field = fields[j];
                    if (jfieldName.ToLower() == field.ToLower())
                    {
                        jUpdatedfields[i]["value"] = fieldsvalue[j];
                        if (jUpdatedfields[i]["id"] != null)
                            jUpdatedfields[i]["id"].Parent.Remove();
                        isFound = true;
                    }
                }

                // Remove the field not being updated
                if (!isFound)
                {
                    jUpdatedfields.RemoveAt(i);
                }
            }

            return JsonConvert.SerializeObject(jUpdated);
        }
    }
}
