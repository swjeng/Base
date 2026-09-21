using Base.Enums;
using Base.Models;
using Base.Services;
using Mongo.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace Mongo.Services
{
    /// <summary>
    /// 不同NoSql缺少一致性，所以這裡不繼承自定介面!! 
    /// MongoDB 基本 CRUD 輔助類別。
    /// 提供連線、建立、讀取、更新、刪除與資源釋放等基礎操作。
    /// </summary>
    public class MgoCrudSvc : IDisposable
    {
        private MongoClient? _client;
        private IMongoDatabase? _db;
        //private IMongoCollection<BsonDocument>? _collection;
        private string _table = "";
        private bool _isOk = false;

        //db str in config file
        private readonly string _dbStr = "";

        public MgoCrudSvc(string dbStr = "")
        {
            _dbStr = dbStr;
        }

        /// <summary>
        /// 依 table 名稱取得(或重新取得)collection，table 改變時會重新指向新的 collection。
        /// </summary>
        private IMongoCollection<BsonDocument>? GetCollection(string table)
        {
            if (_collection != null && _table == table) return;

            if (_client == null)
            {
                var mongoUrl = new MongoUrl(_dbStr);
                _client = new MongoClient(mongoUrl);
                _db = _client.GetDatabase(mongoUrl.DatabaseName);
            }
            _collection = _db!.GetCollection<BsonDocument>(table);
            _table = table;
        }

        /// <summary>
        /// get page rows for dataTables
        /// </summary>
        /// <param name="readDto"></param>
        /// <param name="dtDto"></param>
        /// <param name="ctrl"></param>
        /// <returns></returns>
        public async Task<JObject?> GetPageA(MgoReadDto readDto, DtDto dtDto, string ctrl = "")
        {
            var easyDto = _Model.Copy<DtDto, EasyDtDto>(dtDto);
            if (string.IsNullOrEmpty(dtDto.sort) && dtDto.order != null && dtDto.order.Count > 0)
            {
                //A/D + fidNo(base 0) for jquery dataTables
                easyDto.sort = (dtDto.order![0].dir == OrderTypeEnum.Asc ? "A" : "D") +
                    dtDto.order[0].fid;
            }
            return await GetPageA(readDto, easyDto, ctrl);
        }

        /// <summary>
        /// 依照查詢條件取得 MongoDB 分頁資料。
        /// </summary>
        public async Task<JObject?> GetPageA(MgoReadDto readDto, EasyDtDto dtDto, string ctrl = "")
        {
            if (string.IsNullOrWhiteSpace(readDto.Table))
                return null;

            GetCollection(readDto.Table);

            dtDto.length = Math.Max(0, dtDto.length);
            dtDto.start = Math.Max(0, dtDto.start);

            var filterDocu = string.IsNullOrWhiteSpace(dtDto.findJson)
                ? []
                : BsonDocument.Parse(dtDto.findJson);
            var filter = new BsonDocumentFilterDefinition<BsonDocument>(filterDocu);

            var rowCount = dtDto.recordsFiltered;
            if (rowCount < 0)
                rowCount = (int)await _collection!.CountDocumentsAsync(filter);

            var find = _collection.Find(filter);
            if (!string.IsNullOrWhiteSpace(dtDto.sort) && dtDto.sort.Length > 1)
            {
                var sortField = dtDto.sort[1..];
                var sort = dtDto.sort[0] == 'D'
                    ? Builders<BsonDocument>.Sort.Descending(sortField)
                    : Builders<BsonDocument>.Sort.Ascending(sortField);
                find = find.Sort(sort);
            }

            var docus = await find
                .Skip(dtDto.start)
                .Limit(dtDto.length)
                .ToListAsync();
            var rows = new JArray();
            foreach (var docu in docus)
                rows.Add(JObject.Parse(docu.ToJson()));

            return JObject.FromObject(new
            {
                data = rows,
                recordsFiltered = rowCount,
            });
        }

        public void Dispose()
        {
            //_collection = null;
            _db = null;
            _client = null;
            //_table = "";
            //DbStr = string.Empty;
            //DbName = string.Empty;
            //CollectName = string.Empty;
        }

    }
}
