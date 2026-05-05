using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ECommerceApp
{
    #region 1. INTERFACES
    public interface IShippingStrategy { double CalculateShipping(Order order); string GetStrategyName(); }
    public interface IPaymentStrategy { bool Pay(double amount); string GetMethodName(); }
    public interface IDiscount { double ApplyDiscount(double price); string GetDescription(); }
    #endregion

    #region 2. CORE OOP & ĐA HÌNH
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

    #region 3. DESIGN PATTERNS
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
    #endregion

    #region 4. ORDER & FILE I/O
    public class Order
    {
        public List<Item> Items { get; set; } = new List<Item>();
        public IShippingStrategy Shipping { get; set; } = new StandardShipping();
        public IPaymentStrategy Payment { get; set; } = new CODPayment();
        public IDiscount DiscountProcessor { get; set; } = new BasePrice();

        public void AddItem(Item i) => Items.Add(i);
        public double GetSubTotal() => Items.Sum(x => x.GetSubTotal() + x.GetExtraFee());
        public double GetShippingFee() => Shipping.CalculateShipping(this);
        public double GetFinalTotal() => DiscountProcessor.ApplyDiscount(GetSubTotal() + GetShippingFee());

        // Hợp nhất hàm hiển thị vào đây để tránh lỗi CS1061/CS0103
        public string GetDraftSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n========= TRẠNG THÁI ĐƠN HÀNG =========");
            if (Items.Count == 0) sb.AppendLine("(Giỏ hàng đang trống)");
            else
            {
                sb.AppendLine(string.Format("{0,-20} | {1,-3} | {2,12}", "Tên sản phẩm", "SL", "Thành tiền"));
                sb.AppendLine("------------------------------------------");
                foreach (var i in Items)
                    sb.AppendLine(string.Format("{0,-20} | {1,-3} | {2,12:N0}đ", i.Name, i.Quantity, i.GetSubTotal() + i.GetExtraFee()));
            }
            sb.AppendLine("------------------------------------------");
            sb.AppendLine(string.Format("{0,-28} : {1,10:N0}đ", "Tổng tiền hàng", GetSubTotal()));
            sb.AppendLine(string.Format("{0,-28} : {1,10:N0}đ", "Phí ship (" + Shipping.GetStrategyName() + ")", GetShippingFee()));
            sb.AppendLine(string.Format("{0,-28} : {1}", "Ưu đãi", DiscountProcessor.GetDescription()));
            sb.AppendLine("------------------------------------------");
            sb.AppendLine(string.Format("{0,-28} : {1,10:N0}đ", "TỔNG THANH TOÁN", GetFinalTotal()));
            sb.AppendLine(string.Format("{0,-28} : {1}", "Thanh toán bằng", Payment.GetMethodName()));
            sb.AppendLine("==========================================");
            return sb.ToString();
        }

        public void ExportInvoice()
        {
            string fileName = $"Invoice_{DateTime.Now:yyyyMMddHHmm}.txt";
            File.WriteAllText(fileName, GetDraftSummary(), Encoding.UTF8);
            Console.WriteLine($"\n[Thành công] Hóa đơn đã lưu tại: {fileName}");
        }
    }

    public static class Database
    {
        private const string Path = "Products.csv";
        public static void Initialize()
        {
            if (!File.Exists(Path) || File.ReadAllLines(Path).FirstOrDefault()?.Split(',').Length < 5)
            {
                File.WriteAllText(Path, "id,name,price,type,stock\n1,Binh hoa thuy tinh,150000,FRAGILE,50\n2,Tu go,2500000,BULKY,10\n3,Ao thun LHU,200000,NORMAL,100", Encoding.UTF8);
            }
        }
        public static List<Item> Load()
        {
            var list = new List<Item>();
            foreach (var l in File.ReadAllLines(Path).Skip(1))
            {
                var p = l.Split(','); if (p.Length < 5) continue;
                int id = int.Parse(p[0]); double pr = double.Parse(p[2]); string t = p[3].ToUpper(); int s = int.Parse(p[4]);
                if (t == "FRAGILE") list.Add(new FragileItem(id, p[1], pr, s));
                else if (t == "BULKY") list.Add(new BulkyItem(id, p[1], pr, s));
                else list.Add(new NormalItem(id, p[1], pr, s));
            }
            return list;
        }
        public static void Save(List<Item> inv)
        {
            StringBuilder sb = new StringBuilder("id,name,price,type,stock\n");
            foreach (var i in inv) sb.AppendLine($"{i.Id},{i.Name},{i.Price},{(i is FragileItem ? "FRAGILE" : i is BulkyItem ? "BULKY" : "NORMAL")},{i.Stock}");
            File.WriteAllText(Path, sb.ToString(), Encoding.UTF8);
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
            Database.Initialize(); inventory = Database.Load();

            bool isRunning = true;
            while (isRunning)
            {
                Console.Clear();
                Console.WriteLine(currentOrder.GetDraftSummary()); // Tự động hiện Draft

                Console.WriteLine("======= MENU CỬA HÀNG =======");
                Console.WriteLine("1. Thêm hàng vào giỏ ");
                Console.WriteLine("2. Đổi đơn vị vận chuyển");
                Console.WriteLine("3. Đổi phương thức thanh toán");
                Console.WriteLine("4. Áp dụng mã giảm giá");
                Console.WriteLine("5. Chốt đơn & Xuất hóa đơn");
                Console.WriteLine("0. Thoát");
                Console.Write("\nChọn: ");

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
                        case "0": isRunning = false; break;
                    }
                }
                catch (Exception ex) { Console.WriteLine($"\n[LỖI]: {ex.Message}"); Console.ReadKey(); }
            }
        }

        static void ContinuousBuy()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine(currentOrder.GetDraftSummary());
                Console.WriteLine("--- DANH SÁCH KHO ---");
                foreach (var p in inventory) Console.WriteLine($"{p.Id}. {p.Name} | {p.Price:N0}đ (Kho: {p.Stock})");

                Console.Write("\nNhập ID muốn mua (hoặc 'n' để về Menu): ");
                string input = Console.ReadLine();
                if (input.ToLower() == "n") break;

                if (int.TryParse(input, out int id))
                {
                    var prod = inventory.Find(x => x.Id == id);
                    if (prod != null)
                    {
                        Console.Write($"Nhập SL cho {prod.Name}: ");
                        if (int.TryParse(Console.ReadLine(), out int q) && q > 0 && q <= prod.Stock)
                        {
                            prod.Stock -= q;
                            // Tạo clone để đưa vào giỏ
                            var cartItem = (Item)Activator.CreateInstance(prod.GetType(), prod.Id, prod.Name, prod.Price, prod.Stock);
                            cartItem.Quantity = q;
                            currentOrder.AddItem(cartItem);
                        }
                        else Console.WriteLine("Tồn kho không đủ");
                    }
                    else Console.WriteLine("ID sai!");
                }
                System.Threading.Thread.Sleep(500);
            }
        }

        static void SetShipping()
        {
            Console.WriteLine("\n1. Tiêu chuẩn | 2. Hỏa tốc");
            currentOrder.Shipping = (Console.ReadLine() == "2") ? (IShippingStrategy)new ExpressShipping() : new StandardShipping();
        }

        static void SetPayment()
        {
            Console.WriteLine("\n1. COD | 2. Ví Momo");
            currentOrder.Payment = (Console.ReadLine() == "2") ? (IPaymentStrategy)new MomoPayment() : new CODPayment();
        }

        static void SetDiscount()
        {
            Console.WriteLine("\n1. Voucher 10% | 2. Freeship | 3. Cả hai");
            string c = Console.ReadLine();
            currentOrder.DiscountProcessor = new BasePrice();
            if (c == "1" || c == "3") currentOrder.DiscountProcessor = new VoucherDiscount(currentOrder.DiscountProcessor, 10);
            if (c == "2" || c == "3") currentOrder.DiscountProcessor = new FreeshipDiscount(currentOrder.DiscountProcessor, currentOrder.GetShippingFee());
        }

        static void Checkout()
        {
            if (currentOrder.Items.Count == 0) throw new Exception("Giỏ hàng trống!");
            currentOrder.ExportInvoice();
            Database.Save(inventory);
            currentOrder = new Order();
            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }
    }
    #endregion
}