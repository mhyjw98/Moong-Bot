using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public class ShopManager
    {

        private static DatabaseManager dbManager = new DatabaseManager();
        private static readonly string ShopDataFilePath = Path.Combine("jsonFiles", "shop_items.json");
        private static readonly string StockFilePath = Path.Combine("jsonFiles", "shop_stock.json");
        private static readonly string PurchaseHistoryFilePath = Path.Combine("jsonFiles", "purchase_history.json");

        private Dictionary<ulong, List<string>> _purchaseHistory;
        private List<ShopItem> _items;
        private Dictionary<string, List<string>> _itemStock;

        public ShopManager()
        {
            LoadItemsFromJson();
            LoadItemStock();
            LoadPurchaseHistory();
        }

        // JSON 파일에서 상점 데이터를 불러오는 메서드
        private void LoadItemsFromJson()
        {
            if (File.Exists(ShopDataFilePath))
            {
                var jsonData = File.ReadAllText(ShopDataFilePath);
                _items = JsonConvert.DeserializeObject<List<ShopItem>>(jsonData);
            }
            else
            {
                // 파일이 없으면 기본값으로 초기화
                _items = new List<ShopItem>
                {
                    new ShopItem("띵마카세", 1000000, "2만원 내외로 상품 지급", stock: -1), // 무제한 상품
                    //new ShopItem("니트로 베이직", 250000, "한달간 디스코드 니트로 지급", stock: 3), // 3개 한정
                    //new ShopItem("커피", 20000, "커피 아메리카노 기프티콘 지급", stock: 10) // 10개 한정
                };
                SaveItemsToJson(); // 기본 데이터 저장
            }
        }

        // JSON 파일에 상점 데이터를 저장하는 메서드
        private void SaveItemsToJson()
        {
            var jsonData = JsonConvert.SerializeObject(_items, Formatting.Indented);
            File.WriteAllText(ShopDataFilePath, jsonData);
        }
        public List<ShopItem> GetItems()
        {
            return _items;
        }

        public async Task<(bool purchaseSuccess, string productLink)> PurchaseItem(SocketUser user, string itemName, int userBalance)
        {           
            var item = _items.FirstOrDefault(i => i.Name == itemName);

            if (item == null)
            {
                return (false, "존재하지 않는 아이템입니다.");
            }
            // 구매 이력이 있는지 확인
            if (_purchaseHistory.ContainsKey(user.Id) && _purchaseHistory[user.Id].Contains(itemName))
            {
                return (false, "이미 상품을 구매하였습니다.");
            }
            if (userBalance < item.Price)
            {
                return (false, "잔액이 부족합니다.");
            }
            if((item.Stock == 0 && item.Stock != -1))
            {
                return (false, "품절된 상품입니다.");
            }

            await dbManager.BuyItemAsync(user.Id, item.Price);

            string productLink = null;
            // 재고가 무제한이 아닌 경우 재고를 차감
            if (item.Stock > 0)
            {
                if (_itemStock.ContainsKey(itemName) && _itemStock[itemName].Count > 0)
                {
                    productLink = _itemStock[itemName].First(); // 첫 번째 링크를 가져옴
                    _itemStock[itemName].RemoveAt(0); // 가져온 링크를 재고에서 삭제
                    SaveItemStock(); // 재고 정보 업데이트 후 JSON 파일에 저장
                }                

                // 구매 이력에 추가
                if (!_purchaseHistory.ContainsKey(user.Id))
                {
                    _purchaseHistory[user.Id] = new List<string>();
                }

                _purchaseHistory[user.Id].Add(itemName);
                SavePurchaseHistory(); // 구매 이력 저장

                try
                {
                    _items.First(i => i.Name == itemName).Stock--;
                    SaveItemsToJson(); // 재고 차감 후 JSON에 저장
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine($"Item with name {itemName} not found.");
                }                
            }
                       
            return (true, productLink);
        }

        // JSON으로 저장된 재고 정보를 불러오기
        private void LoadItemStock()
        {
            if (File.Exists(StockFilePath))
            {
                var jsonData = File.ReadAllText(StockFilePath);
                _itemStock = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonData) ?? new Dictionary<string, List<string>>();
            }
            else
            {
                _itemStock = new Dictionary<string, List<string>>
                {
                    { "니트로 베이직", new List<string> { "https://example.com/nitro1", "https://example.com/nitro2", "https://example.com/nitro3" } },
                    { "커피", new List<string> { "https://example.com/coffee1", "https://example.com/coffee2", /* ... */ "https://example.com/coffee10" } }
                };
                SaveItemStock(); // 처음 실행 시 기본 재고 파일 생성
            }
        }

        // 재고 정보를 JSON 파일로 저장
        private void SaveItemStock()
        {
            var jsonData = JsonConvert.SerializeObject(_itemStock, Formatting.Indented);
            File.WriteAllText(StockFilePath, jsonData);
        }

        // 구매 이력 로드
        private void LoadPurchaseHistory()
        {
            if (File.Exists(PurchaseHistoryFilePath))
            {
                var jsonData = File.ReadAllText(PurchaseHistoryFilePath);
                _purchaseHistory = JsonConvert.DeserializeObject<Dictionary<ulong, List<string>>>(jsonData) ?? new Dictionary<ulong, List<string>>();
            }
            else
            {
                _purchaseHistory = new Dictionary<ulong, List<string>>();
            }
        }

        // 구매 이력 저장
        private void SavePurchaseHistory()
        {
            var jsonData = JsonConvert.SerializeObject(_purchaseHistory, Formatting.Indented);
            File.WriteAllText(PurchaseHistoryFilePath, jsonData);
        }
    }
    public class ShopItem
    {
        public string Name { get; set; }
        public int Price { get; set; }
        public string Description { get; set; }
        public int Stock { get; set; }
        public string Link { get; set; }

        public ShopItem(string name, int price, string description, int stock = 0, string link = null)
        {
            Name = name;
            Price = price;
            Description = description;
            Stock = stock;
            Link = link;
        }
    }
}
