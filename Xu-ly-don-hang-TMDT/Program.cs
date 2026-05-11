using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ECommerceApp
{
    #region 1. INTERFACES (STRATEGY & DECORATOR)
    public interface IShippingStrategy { double CalculateShipping(Order order); string GetStrategyName(); }
    public interface IPaymentStrategy { bool Pay(double amount); string GetMethodName(); }
    public interface IDiscount { double ApplyDiscount(double price); string GetDescription(); }
    #endregion

    #region 2. CORE OOP & POLYMORPHISM (ĐA HÌNH)
    public abstract class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public int Stock { get; set; }
        protected Item(int id, string name, double price, int stock) { Id = id; Name = name; Price = price; Stock = stock; Quantity = 0; }
        public virtual double GetSubTotal() => Price * Quantity;
        public abstract double GetExtraFee();
    }
    public class NormalItem : Item { public NormalItem(int id, string name, double price, int stock) : base(id, name, price, stock) { } public override double GetExtraFee() => 0; }
    public class FragileItem : Item { public FragileItem(int id, string name, double price, int stock) : base(id, name, price, stock) { } public override double GetExtraFee() => Price * 0.05; }
    public class BulkyItem : Item { public BulkyItem(int id, string name, double price, int stock) : base(id, name, price, stock) { } public override double GetExtraFee() => 50000 * Quantity; }
    #endregion

    #region 3. DESIGN PATTERNS IMPLEMENTATION
    public class StandardShipping : IShippingStrategy { public double CalculateShipping(Order o) => 30000; public string GetStrategyName() => "Tiêu chuẩn (30k)"; }
    public class ExpressShipping : IShippingStrategy { public double CalculateShipping(Order o) => 60000; public string GetStrategyName() => "Hỏa tốc (60k)"; }
    public class CODPayment : IPaymentStrategy { public bool Pay(double a) => true; public string GetMethodName() => "Tiền mặt (COD)"; }
    public class MomoPayment : IPaymentStrategy { public bool Pay(double a) => true; public string GetMethodName() => "Ví Momo"; }

    public class BasePrice : IDiscount { public double ApplyDiscount(double p) => p; public string GetDescription() => "Giá gốc"; }

    public class VoucherDiscount : IDiscount
    {
        private IDiscount _wrapped; private double _percent;
        public VoucherDiscount(IDiscount wrapped, double percent) { _wrapped = wrapped; _percent = percent; }
        public double ApplyDiscount(double price) => _wrapped.ApplyDiscount(price) * (1 - _percent / 100);
        public string GetDescription() => _wrapped.GetDescription() + $" + Voucher -{_percent}%";
    }

    public class FreeshipDiscount : IDiscount
    {
        private IDiscount _wrapped; private double _shipFee;
        public FreeshipDiscount(IDiscount wrapped, double shipFee) { _wrapped = wrapped; _shipFee = shipFee; }
        public double ApplyDiscount(double price) => Math.Max(_wrapped.ApplyDiscount(price) - _shipFee, 0);
        public string GetDescription() => _wrapped.GetDescription() + " + Freeship";
    }

    // --- NEW: Decorator giảm giá bằng điểm tích lũy ---
    public class PointsDiscount : IDiscount
    {
        private IDiscount _wrapped;
        private int _pointsToUse;
        private const double PointRate = 1000; // 1 điểm = 1000 VNĐ

        public PointsDiscount(IDiscount wrapped, int points) { _wrapped = wrapped; _pointsToUse = points; }
        public double ApplyDiscount(double price) => Math.Max(_wrapped.ApplyDiscount(price) - (_pointsToUse * PointRate), 0);
        public string GetDescription() => _wrapped.GetDescription() + $" + Dùng {_pointsToUse} điểm (-{_pointsToUse * PointRate:N0}đ)";
    }
    #endregion

    #region 4. DATA MODELS & DATABASE
    public class Member
    {
        public string PhoneNumber { get; set; }
        public int Points { get; set; }
        public Member(string phone, int points) { PhoneNumber = phone; Points = points; }
    }

    public class Order
    {
        public List<Item> Items { get; set; } = new List<Item>();
        public IShippingStrategy Shipping { get; set; } = new StandardShipping();
        public IPaymentStrategy Payment { get; set; } = new CODPayment();
        public IDiscount DiscountProcessor { get; set; } = new BasePrice();
        public int PointsUsed { get; set; } = 0; // Lưu số điểm đã dùng để trừ sau checkout

        public void AddItem(Item i) => Items.Add(i);
        public double GetSubTotal() => Items.Sum(x => x.GetSubTotal() + x.GetExtraFee());
        public double GetShippingFee() => Shipping.CalculateShipping(this);
        public double GetFinalTotal() => DiscountProcessor.ApplyDiscount(GetSubTotal() + GetShippingFee());

        public string GetSummary(string title)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\n========= {title} =========");
            if (Items.Count == 0) sb.AppendLine("(Trống)");
            else
            {
                sb.AppendLine(string.Format("{0,-20} | {1,-3} | {2,12}", "Tên sản phẩm", "SL", "Thành tiền"));
                foreach (var i in Items) sb.AppendLine(string.Format("{0,-20} | {1,-3} | {2,12:N0}đ", i.Name, i.Quantity, i.GetSubTotal() + i.GetExtraFee()));
            }
            sb.AppendLine("------------------------------------------");
            sb.AppendLine(string.Format("{0,-28} : {1,10:N0}đ", "Tổng tiền hàng", GetSubTotal()));
            sb.AppendLine(string.Format("{0,-28} : {1,10:N0}đ", "Phí ship (" + Shipping.GetStrategyName() + ")", GetShippingFee()));
            sb.AppendLine(string.Format("{0,-28} : {1}", "Ưu đãi", DiscountProcessor.GetDescription()));
            sb.AppendLine(string.Format("{0,-28} : {1,10:N0}đ", "TỔNG THANH TOÁN", GetFinalTotal()));
            sb.AppendLine(string.Format("{0,-28} : {1}", "Thanh toán bằng", Payment.GetMethodName()));
            sb.AppendLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine("==========================================\n");
            return sb.ToString();
        }
    }

    public static class Database
    {
        private const string ProdFile = "Products.csv";
        private const string HistoryFile = "OrderHistory.txt";
        private const string MemberFile = "Members.csv";

        public static void Initialize()
        {
            if (!File.Exists(ProdFile) || File.ReadAllLines(ProdFile).FirstOrDefault()?.Split(',').Length < 5)
                File.WriteAllText(ProdFile, "id,name,price,type,stock\n1,Binh hoa thuy tinh,150000,FRAGILE,50\n2,Tu go,2500000,BULKY,10\n3,Ao thun LHU,200000,NORMAL,100", Encoding.UTF8);
            if (!File.Exists(HistoryFile)) File.WriteAllText(HistoryFile, "--- LỊCH SỬ GIAO DỊCH ---\n", Encoding.UTF8);
            if (!File.Exists(MemberFile)) File.WriteAllText(MemberFile, "phone,points\n", Encoding.UTF8);
        }

        public static List<Item> LoadItems()
        {
            var list = new List<Item>();
            foreach (var l in File.ReadAllLines(ProdFile).Skip(1))
            {
                var p = l.Split(','); if (p.Length < 5) continue;
                int id = int.Parse(p[0]); double pr = double.Parse(p[2]); string t = p[3].ToUpper(); int s = int.Parse(p[4]);
                if (t == "FRAGILE") list.Add(new FragileItem(id, p[1], pr, s));
                else if (t == "BULKY") list.Add(new BulkyItem(id, p[1], pr, s));
                else list.Add(new NormalItem(id, p[1], pr, s));
            }
            return list;
        }

        public static Member GetMember(string phone)
        {
            var lines = File.ReadAllLines(MemberFile).Skip(1);
            foreach (var l in lines)
            {
                var p = l.Split(',');
                if (p[0] == phone) return new Member(p[0], int.Parse(p[1]));
            }
            return new Member(phone, 0);
        }

        public static void SaveMember(Member m)
        {
            var lines = File.ReadAllLines(MemberFile).ToList();
            bool found = false;
            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(m.PhoneNumber + ","))
                {
                    lines[i] = $"{m.PhoneNumber},{m.Points}";
                    found = true; break;
                }
            }
            if (!found) lines.Add($"{m.PhoneNumber},{m.Points}");
            File.WriteAllLines(MemberFile, lines, Encoding.UTF8);
        }

        public static void SaveStock(List<Item> inv)
        {
            StringBuilder sb = new StringBuilder("id,name,price,type,stock\n");
            foreach (var i in inv) sb.AppendLine($"{i.Id},{i.Name},{i.Price},{(i is FragileItem ? "FRAGILE" : i is BulkyItem ? "BULKY" : "NORMAL")},{i.Stock}");
            File.WriteAllText(ProdFile, sb.ToString(), Encoding.UTF8);
        }

        public static void AppendHistory(string s) => File.AppendAllText(HistoryFile, s, Encoding.UTF8);

        public static string GetFilteredHistory(string phone)
        {
            if (!File.Exists(HistoryFile)) return "Chưa có dữ liệu.";
            string allContent = File.ReadAllText(HistoryFile);
            if (string.IsNullOrWhiteSpace(phone)) return allContent;
            string[] separator = new string[] { "=========" };
            var blocks = allContent.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder("--- KẾT QUẢ TRA CỨU LỊCH SỬ ---\n");
            foreach (var b in blocks)
            {
                if (b.Contains("[ ĐỊNH DANH ]: " + phone)) sb.AppendLine("=========" + b);
            }
            return sb.Length > 40 ? sb.ToString() : "Không tìm thấy lịch sử cho định danh này.";
        }
    }
    #endregion

    #region 5. PROGRAM MAIN
    class Program
    {
        static List<Item> inventory = new List<Item>();
        static Order currentOrder = new Order();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Database.Initialize(); inventory = Database.LoadItems();

            bool isRunning = true;
            while (isRunning)
            {
                Console.Clear();
                Console.WriteLine(currentOrder.GetSummary("GIỎ HÀNG HIỆN TẠI"));
                Console.WriteLine("======= HỆ THỐNG CỬA HÀNG =======");
                Console.WriteLine("1. Mua hàng");
                Console.WriteLine("2. Đổi Phương thức vận chuyển");
                Console.WriteLine("3. Đổi Phương thức thanh toán");
                Console.WriteLine("4. Áp dụng Giảm giá đa tầng");
                Console.WriteLine("5. Thanh toán & Tích điểm");
                Console.WriteLine("6. Xem lịch sử đơn hàng");
                Console.WriteLine("7. Tra cứu điểm tích lũy");
                Console.WriteLine("0. Thoát");
                Console.Write("\nChọn chức năng: ");

                string choice = Console.ReadLine();
                try
                {
                    switch (choice)
                    {
                        case "1": ContinuousBuy(); break;
                        case "2": SetShipping(); break;
                        case "3": SetPayment(); break;
                        case "4": SetDiscount(); break;
                        case "5": Checkout(); break;
                        case "6": ShowHistory(); break;
                        case "7": LookupPoints(); break;
                        case "0": isRunning = false; break;
                        default:
                            Console.WriteLine("\n[LỖI]: Lựa chọn không hợp lệ!");
                            Console.ReadKey();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[LỖI HỆ THỐNG]: {ex.Message}");
                    Console.ReadKey();
                }
            }
        }

        static void ContinuousBuy()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine(currentOrder.GetSummary("GIỎ HÀNG TẠM TÍNH"));
                foreach (var p in inventory) Console.WriteLine($"{p.Id}. {p.Name} | {p.Price,10:N0}đ (Kho: {p.Stock})");
                Console.Write("\nNhập ID muốn mua (hoặc 'n' để quay lại Menu): ");
                string input = Console.ReadLine();
                if (input.ToLower() == "n") break;
                if (int.TryParse(input, out int id))
                {
                    var prod = inventory.Find(x => x.Id == id);
                    if (prod != null)
                    {
                        Console.Write($"SL cho {prod.Name}: ");
                        if (int.TryParse(Console.ReadLine(), out int q) && q > 0 && q <= prod.Stock)
                        {
                            prod.Stock -= q;
                            var cartItem = (Item)Activator.CreateInstance(prod.GetType(), prod.Id, prod.Name, prod.Price, prod.Stock);
                            cartItem.Quantity = q;
                            currentOrder.AddItem(cartItem);
                        }
                        else { Console.WriteLine("SL không hợp lệ! Nhấn phím bất kỳ..."); Console.ReadKey(); }
                    }
                    else { Console.WriteLine("ID không tồn tại! Nhấn phím bất kỳ..."); Console.ReadKey(); }
                }
            }
        }

        static void SetShipping()
        {
            Console.WriteLine("\n1. Tiêu chuẩn | 2. Hỏa tốc");
            string s = Console.ReadLine();
            currentOrder.Shipping = (s == "2") ? (IShippingStrategy)new ExpressShipping() : new StandardShipping();
        }

        static void SetPayment()
        {
            Console.WriteLine("\n1. COD | 2. Ví Momo");
            string p = Console.ReadLine();
            currentOrder.Payment = (p == "2") ? (IPaymentStrategy)new MomoPayment() : new CODPayment();
        }

        static void SetDiscount()
        {
            Console.WriteLine("\n--- ÁP DỤNG GIẢM GIÁ ---");
            Console.WriteLine("1. Voucher 10% | 2. Freeship | 3. Voucher 10% và Freeship | 4. Dùng điểm tích lũy");
            string c = Console.ReadLine();
            currentOrder.DiscountProcessor = new BasePrice(); // Reset
            currentOrder.PointsUsed = 0; // Reset điểm dùng

            if (c == "1" || c == "3") currentOrder.DiscountProcessor = new VoucherDiscount(currentOrder.DiscountProcessor, 10);
            if (c == "2" || c == "3") currentOrder.DiscountProcessor = new FreeshipDiscount(currentOrder.DiscountProcessor, currentOrder.GetShippingFee());

            if (c == "4")
            {
                Console.Write("Nhập SĐT khách hàng để kiểm tra điểm: ");
                string phone = Console.ReadLine();
                Member m = Database.GetMember(phone);
                if (m.Points > 0)
                {
                    Console.WriteLine($"Khách hàng hiện có: {m.Points} điểm (1 điểm = 1.000đ)");
                    Console.Write("Nhập số điểm muốn dùng: ");
                    if (int.TryParse(Console.ReadLine(), out int pts) && pts > 0 && pts <= m.Points)
                    {
                        currentOrder.PointsUsed = pts;
                        currentOrder.DiscountProcessor = new PointsDiscount(currentOrder.DiscountProcessor, pts);
                        Console.WriteLine("Đã áp dụng giảm giá bằng điểm!");
                    }
                    else Console.WriteLine("Số điểm không hợp lệ.");
                }
                else Console.WriteLine("Khách hàng chưa có điểm.");
                Console.ReadKey();
            }
        }

        static void Checkout()
        {
            if (currentOrder.Items.Count == 0) throw new Exception("Giỏ hàng đang trống!");

            string phone = "KHACH_LE";
            Console.Write("\nBạn có muốn tích điểm không? (y/n): ");
            if (Console.ReadLine().ToLower() == "y")
            {
                Console.Write("Nhập Số điện thoại/Mã định danh: ");
                phone = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(phone)) phone = "KHACH_LE";
            }

            int earned = (int)(currentOrder.GetFinalTotal() / 10000);
            Member m = Database.GetMember(phone);

            // Xử lý trừ điểm (nếu có dùng) và cộng điểm mới
            if (phone != "KHACH_LE")
            {
                // Nếu khách hàng trong đơn là khách hàng dùng điểm lúc nãy
                m.Points -= currentOrder.PointsUsed;
                m.Points += earned;
                Database.SaveMember(m);
            }

            string s = currentOrder.GetSummary("HÓA ĐƠN THANH TOÁN");
            s += $"[ ĐỊNH DANH ]: {phone}\n[ ĐIỂM DÙNG ]: -{currentOrder.PointsUsed} pts\n[ ĐIỂM NHẬN ]: +{earned} pts\n";
            if (phone != "KHACH_LE") s += $"[ TỔNG ĐIỂM HIỆN CÓ ]: {m.Points} pts\n";

            Database.AppendHistory(s);
            File.WriteAllText($"Invoice_{DateTime.Now:yyyyMMddHHmm}.txt", s, Encoding.UTF8);
            Database.SaveStock(inventory);
            currentOrder = new Order();
            Console.WriteLine("\nThanh toán thành công! Nhấn phím bất kỳ...");
            Console.ReadKey();
        }

        static void ShowHistory()
        {
            Console.Clear();
            Console.Write("Nhập SĐT/Mã định danh để lọc lịch sử (Để trống để xem tất cả): ");
            string filter = Console.ReadLine();
            Console.WriteLine(Database.GetFilteredHistory(filter));
            Console.WriteLine("\nNhấn phím bất kỳ để quay lại...");
            Console.ReadKey();
        }

        static void LookupPoints()
        {
            Console.Clear();
            Console.Write("Nhập Số điện thoại/Mã định danh cần tra cứu: ");
            string phone = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(phone))
            {
                Console.WriteLine("Vui lòng không để trống mã định danh.");
            }
            else
            {
                Member m = Database.GetMember(phone);
                Console.WriteLine("\n------------------------------------------");
                Console.WriteLine($"Định danh: {phone}");
                Console.WriteLine($"Số điểm tích lũy: {m.Points} pts");
                Console.WriteLine("------------------------------------------");
            }
            Console.WriteLine("Nhấn phím bất kỳ để quay lại...");
            Console.ReadKey();
        }
    }
    #endregion
}