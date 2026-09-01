using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language
{
    Vietnamese,
    English
}

public static class Localization
{
    private const string PlayerPrefsKey = "Language";

    private static Language _current = Language.Vietnamese;

    public static event Action OnLanguageChanged;

    public static Language Current
    {
        get => _current;
        private set => _current = value;
    }

    static Localization()
    {
        _current = (Language)PlayerPrefs.GetInt(PlayerPrefsKey, (int)Language.Vietnamese);
    }

    public static void SetLanguage(Language lang)
    {
        if (_current == lang) return;
        _current = lang;
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    public static void ToggleLanguage()
    {
        SetLanguage(Current == Language.Vietnamese ? Language.English : Language.Vietnamese);
    }

    public static string T(string vn)
    {
        if (string.IsNullOrEmpty(vn)) return vn;
        if (Current == Language.English && Translations.TryGetValue(vn, out var en))
            return en;
        return vn;
    }

    public static string F(string vnPattern, params object[] args)
    {
        return string.Format(T(vnPattern), args);
    }

    public static string ItemName(string itemType)
    {
        if (string.IsNullOrEmpty(itemType)) return itemType;
        if (ItemNames.TryGetValue(itemType, out var vn))
            return T(vn);
        return itemType;
    }

    public static string BuildingName(string buildingKey)
    {
        if (string.IsNullOrEmpty(buildingKey)) return buildingKey;
        if (BuildingNames.TryGetValue(buildingKey, out var vn))
            return T(vn);
        return buildingKey;
    }

    public static string MansionPartName(string partName)
    {
        if (string.IsNullOrEmpty(partName)) return partName;
        if (MansionParts.TryGetValue(partName, out var vn))
            return T(vn);
        return partName;
    }

    public static string AnimalName(string animalType)
    {
        if (string.IsNullOrEmpty(animalType)) return animalType;
        if (AnimalNames.TryGetValue(animalType, out var vn))
            return T(vn);
        return animalType;
    }

    public static string DaySuffix(int day)
    {
        return Current == Language.English ? "Day" : "Ngày";
    }

    public static string QuestName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        string body = raw;
        string daySuffix = "";
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"^(.*?) \[Ngày (\d+)\]$");
        if (m.Success)
        {
            body = m.Groups[1].Value;
            daySuffix = " " + F("[Ngày {0}]", m.Groups[2].Value);
        }
        if (body.StartsWith("[Hàng Ngày] "))
            return "[" + T("Hàng Ngày") + "] " + T(body.Substring("[Hàng Ngày] ".Length)) + daySuffix;
        if (body.StartsWith("[Giới Hạn] "))
            return "[" + T("Giới Hạn") + "] " + T(body.Substring("[Giới Hạn] ".Length)) + daySuffix;
        return T(body) + daySuffix;
    }

    private static readonly Dictionary<string, string> ItemNames = new Dictionary<string, string>
    {
        // Tools
        { "axe", "Rìu" },
        { "pickaxe", "Cuốc Chim" },
        { "hoe", "Cuốc" },
        { "hammer", "Búa" },
        { "scythe", "Lưỡi Hái" },
        { "sickle", "Lưỡi Hái" },
        { "watering_can", "Bình Tưới" },
        { "fishing_rod", "Cần Câu" },
        { "fishing_bait", "Mồi Câu" },
        { "fishing_chum", "Mồi Bả" },
        { "rosary", "Tràng Hạt" },
        { "club", "Cây Gậy" },
        { "mi_hao_hao", "Mì Hảo Hảo" },
        { "com_trang", "Cơm Trắng" },
        { "com_tam", "Cơm Tấm" },
        { "com_ga", "Cơm Gà" },
        { "com_chieu", "Cơm Chiên" },
        { "cage_big", "Lồng Lớn" },
        { "cage_small", "Lồng Nhỏ" },

        // Convenience store
        { "nuoc_dau", "Nước Dừa" },
        { "tra_da", "Trà Đá" },
        { "soda", "Soda" },
        { "banh_mi", "Bánh Mì" },
        { "banh_tet", "Bánh Tét" },
        { "keo", "Kẹo" },

        // Cafe
        { "cafe_den", "Cà Phê Đen" },

        // Quest tips
        { "--- Gợi Ý ---", "--- Tips ---" },
        { "Mở khóa ngày {0}: {1}", "Unlocks on day {0}: {1}" },
        { "Bạn đã hoàn thành mọi nhiệm vụ cốt truyện. Hãy khám phá các kết thúc khác của câu chuyện!", "You've completed every story quest. Explore the other endings of the story!" },
        { "Gợi ý: Nói chuyện với Jessica mỗi ngày và hoàn thành nhiệm vụ của cô ấy để tăng Độ Thân Mật. Đạt 70+ để cầu hôn.", "Tip: Talk to Jessica every day and complete her quests to raise Affection. Reach 70+ to propose." },

        // Grocery store
        { "tu_gao", "Túi Gạo" },
        { "duong", "Đường" },
        { "muoi", "Muối" },
        { "xap_phong", "Xà Phòng" },
        { "mi_chinh", "Mì Chính" },

        // Seeds
        { "wheat_seed", "Hạt Giống Lúa Mì" },
        { "corn_seed", "Hạt Giống Ngô" },
        { "peashooter_seed", "Hạt Giống Đậu" },
        { "potato_seed", "Hạt Giống Khoai Tây" },
        { "carrot_seed", "Hạt Giống Cà Rốt" },
        { "tomato_seed", "Hạt Giống Cà Chua" },
        { "strawberry_seed", "Hạt Giống Dâu Tây" },
        { "pumpkin_seed", "Hạt Giống Bí Ngòi" },
        { "onion_seed", "Hạt Giống Hành Tây" },
        { "sugarcane_seed", "Hạt Giống Mía" },
        { "rice_seed", "Hạt Giống Gạo" },

        // Crops
        { "wheat", "Lúa Mì" },
        { "corn", "Ngô" },
        { "potato", "Khoai Tây" },
        { "carrot", "Cà Rốt" },
        { "tomato", "Cà Chua" },
        { "strawberry", "Dâu Tây" },
        { "pumpkin", "Bí Ngòi" },
        { "onion", "Hành Tây" },
        { "sugarcane", "Mía" },
        { "rice", "Gạo" },

        // Animal products
        { "egg", "Trứng" },
        { "milk", "Sữa" },
        { "wool", "Len" },
        { "meat", "Thịt" },
        { "feather", "Lông Vũ" },
        { "honey", "Mật Ong" },

        // Damaged crops
        { "damaged_wheat", "Lúa Mì Hư" },
        { "damaged_corn", "Ngô Hư" },
        { "damaged_potato", "Khoai Tây Hư" },
        { "damaged_carrot", "Cà Rốt Hư" },
        { "damaged_tomato", "Cà Chua Hư" },
        { "damaged_strawberry", "Dâu Tây Hư" },
        { "damaged_pumpkin", "Bí Ngòi Hư" },
        { "damaged_onion", "Hành Tây Hư" },
        { "damaged_sugarcane", "Mía Hư" },
        { "damaged_rice", "Gạo Hư" },

        // Fish
        { "fish_carp", "Cá Chép" },
        { "fish_salmon", "Cá Hồi" },
        { "fish_tuna", "Cá Ngừ" },
        { "fish_pufferfish", "Cá Nóc" },

        // Materials
        { "wood", "Gỗ" },
        { "stone", "Đá" },

        // Items
        { "fertilizer", "Phân Bón" },
        { "peashooter", "Đậu Bắn" },
        { "torch", "Đuốc" },
        { "crop", "Nông Sản" },

        // Crafted goods
        { "xoi_gac", "Xôi Gấc" },
        { "sup_bi_ngo", "Súp Bí Ngòi" },
        { "mut_ca_rot", "Mứt Cà Rốt" },
        { "trai_cay_kho", "Trái Cây Khô" },
        { "dua_chua", "Dưa Chua" },
        { "ruou_gao", "Rượu Gạo" },
        { "tuong_ot", "Tương Ớt" },
        { "ruou_tang", "Rượu Thuốc" },
        { "tinh_duoc", "Tinh Dược" },

        // Enemy materials
        { "demon_horn", "Sừng Quỷ" },
        { "dark_essence", "Tinh Chất Bóng Tối" },
        { "bone", "Xương Quái Vật" }
    };

    private static readonly Dictionary<string, string> BuildingNames = new Dictionary<string, string>
    {
        { "wood_wall", "Tường Gỗ" },
        { "stone_wall", "Tường Đá" },
        { "fence", "Hàng Rào" },
        { "watchtower", "Lính Canh" },
        { "small_house", "Nhà Nhỏ" },
        { "wood_floor", "Sàn Gỗ" },
        { "stone_floor", "Sàn Đá" },
        { "stair", "Cầu Thang" },
        { "table", "Bàn" },
        { "chair", "Ghế" },
        { "sofa", "Ghế Sofa" },
        { "door", "Cửa" },
        { "wife_house", "Nhà Vợ" },
        { "structure_house", "Nhà Cấu Trúc" },
        { "goblin_hut", "Túp Lều Goblin" },
        { "library", "Thư Viện" },
        { "well", "Giếng Nước" },
        { "fountain", "Đài Phun Nước" },
        { "workshop", "Xưởng Chế Tạo" },
        { "crafting_stove", "Bếp Nấu" },
        { "preserve_jar", "Lọ Ngâm" },
        { "brewing_kettle", "Nồi Ủ" },
        { "chest", "Rương Đồ" }
    };

    private static readonly Dictionary<string, string> MansionParts = new Dictionary<string, string>
    {
        { "Mansion", "Dinh Thự" },
        { "Mansion_Foundation", "Nền" },
        { "Mansion_PorchSlab", "Sân Trước" },
        { "Mansion_BackPatio", "Sân Sau" },
        { "Mansion_1F_Floor", "Sàn Tầng 1" },
        { "Mansion_1F_ExteriorWalls", "Tường Bên Ngoài T1" },
        { "Mansion_1F_InteriorWalls", "Tường Bên Trong T1" },
        { "Mansion_FrontDoor", "Cửa Chính" },
        { "Mansion_LivingRoom", "Phòng Khách" },
        { "Mansion_Kitchen", "Nhà Bếp" },
        { "Mansion_DiningRoom", "Phòng Ăn" },
        { "Mansion_Bathroom1F", "Phòng Tắm T1" },
        { "Mansion_2F_Floor", "Sàn Tầng 2" },
        { "Mansion_2F_ExteriorWalls", "Tường Bên Ngoài T2" },
        { "Mansion_2F_InteriorWalls", "Tường Bên Trong T2" },
        { "Mansion_Staircase", "Cầu Thang" },
        { "Mansion_MasterBedroom", "Phòng Ngủ Chính" },
        { "Mansion_Bedroom2", "Phòng Ngủ 2" },
        { "Mansion_Bedroom3", "Phòng Ngủ 3" },
        { "Mansion_Bathroom2F", "Phòng Tắm T2" },
        { "Mansion_HallwayDecor", "Hành Lang" },
        { "Mansion_MainRoof", "Mái Chính" },
        { "Mansion_PorchRoof", "Mái Sân Trước" },
        { "Mansion_Balcony", "Ban Công" },
        { "Mansion_GardenPath", "Lối Vào" },
        { "Mansion_Fence", "Hàng Rào" }
    };

    private static readonly Dictionary<string, string> AnimalNames = new Dictionary<string, string>
    {
        { "Cow", "Bò" },
        { "Pig", "Lợn" },
        { "Sheep", "Cừu" },
        { "Goat", "Dê" },
        { "Chicken", "Gà" },
        { "Duck", "Vịt" },
        { "Turkey", "Gà Tây" }
    };

    private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>
    {
        // UI / HUD
        { "Ngày {0} - {1}", "Day {0} - {1}" },
        { "Thể Lực: {0}/{1}", "Stamina: {0}/{1}" },
        { "Tiền: {0}", "Money: {0}" },
        { "HP: {0}/{1}", "HP: {0}/{1}" },
        { "Túi đồ đầy.", "Inventory is full." },
        { "Đã chọn: {0}. Nhấp để đặt.", "Selected: {0}. Click to place." },
        { "Đã lưu trò chơi!", "Game saved!" },
        { "Không tìm thấy file lưu!", "Save file not found!" },
        { "Không đọc được file lưu!", "Cannot read save file!" },
        { "Đã tải trò chơi!", "Game loaded!" },

        // Menus / panels
        { "Tiếp Tục", "Continue" },
        { "Lưu Game", "Save Game" },
        { "Tải Game", "Load Game" },
        { "Trống", "Empty" },
        { "Chơi: {0}", "Played: {0}" },
        { "Thống Kê", "Statistics" },
        { "Nhiệm Vụ", "Quests" },
        { "Cài Đặt", "Settings" },
        { "Hướng Dẫn", "Tutorial" },
        { "Thoát", "Quit" },
        { "THỐNG KÊ", "STATISTICS" },
        { "Quay Lại", "Back" },
        { "CÀI ĐẶT", "SETTINGS" },
        { "ĐỘ NHẠY CHUỘT", "MOUSE SENSITIVITY" },
        { "ĐỘ NHẠY CẢM ỨNG", "TOUCH SENSITIVITY" },
        { "Đảo Trục Dọc: BẬT", "Invert Y: ON" },
        { "Đảo Trục Dọc: TẮT", "Invert Y: OFF" },
        { "CÁCH ĐIỀU KHIỂN", "CONTROL MODE" },
        { "PC / Bàn Phím", "PC / Keyboard" },
        { "Điện Thoại / Cảm Ứng", "Mobile / Touch" },
        { "Đóng", "Close" },
        { "Goblin", "Goblin" },
        { "Máu: {0}/{1}", "HP: {0}/{1}" },
        { "Điều khiển", "Command" },
        { "Ra lệnh cho goblin...", "Command the goblin..." },
        { "Theo Dõi", "Follow" },
        { "Đứng Yên", "Stay" },
        { "Về Nhà", "Go Home" },
        { "[Theo Dõi] (Chạm)", "[Follow] (Tap)" },
        { "[Theo Dõi] Ấn 1", "[Follow] Press 1" },
        { "[Đứng Yên] (Chạm)", "[Stay] (Tap)" },
        { "[Đứng Yên] Ấn 2", "[Stay] Press 2" },
        { "[Về Nhà] (Chạm)", "[Go Home] (Tap)" },
        { "[Về Nhà] Ấn 3", "[Go Home] Press 3" },
        { "[Đóng] (Chạm)", "[Close] (Tap)" },
        { "[Đóng] Ấn E", "[Close] Press E" },
        { "Đã chết (hồi sinh vào ban ngày)", "Dead (revives at daybreak)" },
        { "Đang trốn trong chuồng", "Hiding in the hut" },
        { "Đang gieo hạt: {0}", "Planting: {0}" },
        { "Đứng yên tại chỗ", "Staying put" },
        { "Đang về nhà nghỉ ngơi", "Heading home to rest" },
        { "Đang theo dõi chủ nhân", "Following the owner" },
        { "Goblin đã nhận lệnh.", "The goblin got the order." },
        { "NHIỆM VỤ", "QUESTS" },
        { "XÂY DỰNG NÔNG TRẠI", "BUILD YOUR FARM" },
        { "Trò Mới", "New Game" },
        { "Tiếp Tục (Tải)", "Continue (Load)" },
        { "Xem Giới Thiệu", "Watch Intro" },
        { "Bỏ Qua Giới Thiệu", "Skip Intro" },
        { "Chọn Giới Tính", "Choose Gender" },
        { "Nam", "Male" },
        { "Nữ", "Female" },
        { "Chỉ là ngoại hình, không ảnh hưởng trò chơi.", "Cosmetic only - does not affect gameplay." },
        { "Chơi Lại", "Play Again" },
        { "Chọn thiết bị bạn sẽ chơi", "Choose the device you will play on" },
        { "Lúa đã thu hoạch: {0}", "Wheat harvested: {0}" },
        { "Kẻ thù đã diệt: {0}", "Enemies defeated: {0}" },
        { "Tiền đã kiếm: {0}", "Money earned: {0}" },
        { "Tiền bị cướp: {0}", "Money stolen: {0}" },

        // Language settings
        { "Ngôn Ngữ", "Language" },
        { "Tiếng Việt", "Tiếng Việt" },
        { "English", "English" },

        // Shops
        { "Cửa Hàng Bà Tân", "Mrs. Tan's Shop" },
        { "Cửa Hàng Trâu", "Buffalo Shop" },
        { "Cửa Hàng Nông Cụ", "Tool Shop" },
        { "Cửa Hàng Tiện Lợi", "Convenience Store" },
        { "Cửa Hàng Tạp Hóa", "Grocery Store" },
        { "Mua", "Buy" },
        { "Bán", "Sell" },
        { "Bán Tất Cả", "Sell All" },
        { "{0} · Trang {1}/{2}", "{0} · Page {1}/{2}" },
        { "Không đủ tiền", "Not enough money" },
        { "Túi đồ đầy", "Inventory is full" },
        { "Đã mua {0}", "Bought {0}" },
        { "Không có {0} để bán", "No {0} to sell" },
        { "Đã bán {0} {1} (+{2}g)", "Sold {0} {1} (+{2}g)" },
        { "Đã bán tất cả (+{0}g)", "Sold everything (+{0}g)" },
        { "Không có gì để bán", "Nothing to sell" },
        { "Chế Tạo", "Craft" },
        { "Thiếu nguyên liệu.", "Missing ingredients." },
        { "Đã chế tạo: {0}", "Crafted: {0}" },
        { "Đã chế tạo: ", "Crafted: " },
        { "Đã ăn {0}. Hồi phục +{1} Thể Lực và hồi Thể Lực nhanh hơn 20% trong 120 giây!", "Ate {0}. +{1} Stamina and stamina regenerates 20% faster for 120 seconds!" },

        // Shop item labels
        { "Hạt Lúa Mì", "Wheat Seed" },
        { "Hạt Ngô", "Corn Seed" },
        { "Hạt Cà Rốt", "Carrot Seed" },
        { "Hạt Cà Chua", "Tomato Seed" },
        { "Hạt Dâu Tây", "Strawberry Seed" },
        { "Hạt Bí Ngòi", "Pumpkin Seed" },
        { "Hạt Hành Tây", "Onion Seed" },
        { "Hạt Mía", "Sugarcane Seed" },
        { "Hạt Gạo", "Rice Seed" },
        { "Phân Bón", "Fertilizer" },
        { "Bình Tưới", "Watering Can" },
        { "Gậy", "Club" },
        { "Lồng Lớn", "Big Cage" },
        { "Lồng Nhỏ", "Small Cage" },
        { "Rìu", "Axe" },
        { "Cuốc Chim", "Pickaxe" },
        { "Cuốc", "Hoe" },
        { "Mì Hảo Hảo", "Instant Noodles" },
        { "Cần Câu", "Fishing Rod" },
        { "Tràng Hạt", "Rosary" },
        { "Nước Dừa", "Coconut Water" },
        { "Trà Đá", "Iced Tea" },
        { "Soda", "Soda" },
        { "Bánh Mì", "Bread" },
        { "Bánh Tét", "Sticky Rice Cake" },
        { "Kẹo", "Candy" },
        { "Túi Gạo", "Bag of Rice" },
        { "Đường", "Sugar" },
        { "Muối", "Salt" },
        { "Xà Phòng", "Soap" },
        { "Mì Chính", "MSG" },
        { "Lúa Mì", "Wheat" },
        { "Lúa Mì Hư", "Damaged Wheat" },
        { "Ngô", "Corn" },
        { "Ngô Hư", "Damaged Corn" },
        { "Khoai Tây", "Potato" },
        { "Khoai Tây Hư", "Damaged Potato" },
        { "Cà Rốt", "Carrot" },
        { "Cà Rốt Hư", "Damaged Carrot" },
        { "Cà Chua", "Tomato" },
        { "Cà Chua Hư", "Damaged Tomato" },
        { "Dâu Tây", "Strawberry" },
        { "Dâu Tây Hư", "Damaged Strawberry" },
        { "Bí Ngòi", "Pumpkin" },
        { "Bí Ngòi Hư", "Damaged Pumpkin" },
        { "Hành Tây", "Onion" },
        { "Hành Tây Hư", "Damaged Onion" },
        { "Mía", "Sugarcane" },
        { "Mía Hư", "Damaged Sugarcane" },
        { "Gạo", "Rice" },
        { "Gạo Hư", "Damaged Rice" },
        { "Cá Chép", "Carp" },
        { "Cá Hồi", "Salmon" },
        { "Cá Ngừ", "Tuna" },
        { "Cá Nóc", "Pufferfish" },

        // ToolManager messages
        { "Quá mệt!", "Too tired!" },
        { "Đã huỷ đặt công trình.", "Cancelled building placement." },
        { "Đang mang: Lồng với {0} (Q để ném)", "Carrying: Cage with {0} (Q to throw)" },
        { "Đang mang: {0} {1}", "Carrying: {0} {1}" },
        { "Cần: {0}", "Need: {0}" },
        { "Hoàn thành!", "Complete!" },
        { "Dinh Thự - {0} - {1}", "Mansion - {0} - {1}" },
        { "Lồng với {0} (E để nhặt)", "Cage with {0} (E to pick up)" },
        { "Lồng (E để nhặt)", "Cage (E to pick up)" },
        { "Cây", "Tree" },
        { "Cành", "Branch" },
        { "Mảnh Vụn", "Debris" },
        { "{0} cung cấp {1} {2}", "{0} provides {1} {2}" },
        { "Quá nhỏ để dùng.", "Too small to use." },
        { "Xây dựng hoàn thành!", "Building complete!" },
        { "Đã cung cấp {0} x{1}.", "Provided {0} x{1}." },
        { "Ruộng đã cày.", "Field tilled." },
        { "Cần sàn! Tường và cầu thang cần sàn trước.", "Need a floor! Walls and stairs need a floor first." },
        { "Bản thiết kế đã đặt. Cung cấp gỗ & đá.", "Blueprint placed. Provide wood & stone." },
        { "Không thể đặt ở đây.", "Cannot place here." },
        { "Ruộng đã tưới.", "Field watered." },
        { "Dùng bình tưới cho cây đang trồng.", "Use the watering can on planted crops." },
        { "Ruộng đã bón phân!", "Field fertilized!" },
        { "Dùng phân bón cho cây đang trồng.", "Use fertilizer on planted crops." },
        { "Dùng mì chính cho cây đang trồng.", "Use MSG on planted crops." },
        { "Máu đã đầy!", "HP already full!" },
        { "Đã thu hoạch {0}.", "Harvested {0}." },
        { "Đã gieo {0}.", "Planted {0}." },
        { "Dùng hạt giống trên đất đã cày.", "Use seeds on tilled soil." },
        { "Chọn hạt giống để đưa cho goblin.", "Select a seed to give to the goblin." },
        { "Goblin đang bất tỉnh!", "The goblin is knocked out!" },
        { "Goblin đang bận!", "The goblin is busy!" },
        { "Không thể lấy hạt giống.", "Cannot take seed." },
        { "Đã đưa hạt giống cho goblin.", "Gave a seed to the goblin." },
        { "Đã nhặt lồng với {0}.", "Picked up cage with {0}." },
        { "Đã nhặt lên.", "Picked up." },
        { "Cá đang quẫy! Dùng gậy gõ cho nó xỉu.", "The fish is thrashing! Hit it with a club to knock it out." },
        { "Đã nhặt {0}.", "Picked up {0}." },
        { "Đã ném {0}.", "Threw {0}." },
        { "Đang ném lồng...", "Throwing cage..." },
        { "Đã ném lồng trống.", "Threw empty cage." },
        { "Đã bỏ xuống.", "Put down." },
        { "Xây Dựng", "Building" },

        // Quests
        { "Nhiệm Vụ: Sẵn sàng", "Quest: Ready" },
        { "Nhiệm vụ hoàn thành! Nhận {0}g!", "Quest complete! Received {0}g!" },
        { "{0} thất bại!", "{0} failed!" },
        { "THẤT BẠI", "FAILED" },
        { "HOÀN THÀNH", "COMPLETE" },
        { "Hàng Ngày", "Daily" },
        { "Giới Hạn", "Timed" },
        { "--- Nhiệm Vụ Cốt Truyện ---", "--- Story Quests ---" },
        { "--- Nhiệm Vụ Hàng Ngày ---", "--- Daily Quests ---" },
        { "--- Nhiệm Vụ Giới Hạn ---", "--- Timed Quests ---" },

        // Quest names & descriptions
        { "Chào Hỏi Hàng Xóm", "Greet The Neighbors" },
        { "Nói chuyện với Jessica để làm quen với hàng xóm.", "Talk to Jessica to get to know your neighbor." },
        { "Mùa Thu Đầu Tiên", "First Autumn" },
        { "Thu hoạch 50 lúa mì để trở thành nông dân thực thụ.", "Harvest 50 wheat to become a true farmer." },
        { "Bảo Vệ Đất", "Protect The Land" },
        { "Diệt 10 kẻ thù để bảo vệ nông trại.", "Defeat 10 enemies to protect the farm." },
        { "Bàn Tay Xanh", "Green Thumb" },
        { "Thu hoạch 150 lúa mì để chứng minh tài năng.", "Harvest 150 wheat to prove your talent." },
        { "Xây Dựng Đế Chế", "Build An Empire" },
        { "Kiếm 50.000 vàng bằng cách bán nông sản.", "Earn 50,000 gold by selling crops." },
        { "Thợ Săn Quái Vật", "Monster Hunter" },
        { "Diệt 30 kẻ thù để làm sạch vùng đất.", "Defeat 30 enemies to cleanse the land." },
        { "Trận Đấu Cuối Cùng", "The Final Battle" },
        { "Diệt 50 kẻ thù — trận chiến sinh tử!", "Defeat 50 enemies — a battle for survival!" },
        { "Tỷ Phú", "Billionaire" },
        { "Kiếm 200.000 vàng để trở thành tỷ phú.", "Earn 200,000 gold to become a billionaire." },
        { "Thu Hoạch Nhanh", "Quick Harvest" },
        { "Thu hoạch 25 lúa mì hôm nay.", "Harvest 25 wheat today." },
        { "Mùa Màng Bội Thu", "Bumper Crop" },
        { "Thu hoạch 60 lúa mì hôm nay.", "Harvest 60 wheat today." },
        { "Tiêu Diệt Sâu Bệnh", "Pest Control" },
        { "Diệt 5 quái vật hôm nay.", "Defeat 5 monsters today." },
        { "Săn Quái", "Hunting" },
        { "Diệt 15 quái vật hôm nay.", "Defeat 15 monsters today." },
        { "Kiếm Thêm", "Extra Income" },
        { "Kiếm 5.000 vàng hôm nay.", "Earn 5,000 gold today." },
        { "Thu Nhập Lớn", "Big Income" },
        { "Kiếm 15.000 vàng hôm nay.", "Earn 15,000 gold today." },
        { "Chuỗi Lúa Mì", "Wheat Chain" },
        { "Thu hoạch 100 lúa mì hôm nay.", "Harvest 100 wheat today." },
        { "Diệt Sạch", "Wipe Them Out" },
        { "Diệt 25 quái vật hôm nay.", "Defeat 25 monsters today." },

        // Wife / Jessica quests
        { "[Ngày {0}]", "[Day {0}]" },
        { "Xây Dựng Dinh Thự Cho Jessica", "Build A Mansion For Jessica" },
        { "Jessica đồng ý lời tỏ tình! Hãy xây dinh thự để làm lễ cưới.", "Jessica accepted your confession! Build the mansion for your wedding." },
        { "Trừ Tà Giúp Làng", "Exorcise For The Village" },
        { "Dùng Tràng Hạt tiêu diệt 5 con quỷ để bảo vệ làng.", "Use the Rosary to defeat 5 demons and protect the village." },
        { "Câu Cá Lần Đầu", "First Fishing Trip" },
        { "Jessica nhờ anh câu 3 con cá. Cô ấy đã tặng anh chiếc cần câu để bắt đầu!", "Jessica asks you to catch 3 fish. She gave you a fishing rod to start!" },
        { "Thu Hoạch Lúa Mì", "Harvest Wheat" },
        { "Thu Thập Trứng", "Collect Eggs" },
        { "Trồng Cà Rốt", "Plant Carrots" },
        { "Tưới Nước Cho Cây", "Water The Plants" },
        { "Câu Cá", "Go Fishing" },
        { "Ném Lúa Mì Cho Jessica", "Throw Wheat For Jessica" },
        { "Ném Cà Rốt Cho Jessica", "Throw Carrots For Jessica" },
        { "Ném Gỗ Cho Jessica", "Throw Wood For Jessica" },
        { "Ném Đá Cho Jessica", "Throw Stone For Jessica" },
        { "Ném Lồng Thú Cho Jessica", "Throw Animal Cages For Jessica" },
        { "Lồng Thú", "Animal Cage" },
        { "Đã nộp {0} cho Jessica! ({1}/{2})", "Delivered {0} to Jessica! ({1}/{2})" },
        { "Nhấn Q để ném {0} vào rổ của Jessica!", "Press Q to throw {0} into Jessica's basket!" },
        { "Nhiệm vụ hàng ngày từ Jessica. Hoàn thành trước 6h sáng mai!", "A daily task from Jessica. Complete it before 6 AM tomorrow!" },
        { "Bỏ vật phẩm nhiệm vụ hàng ngày vào đây", "Put the daily quest material here" },

        // Wife dialogs
        { "Anh ơi... em thực sự rất bất ngờ!", "Oh honey... I'm truly surprised!" },
        { "Em cũng có tình cảm với anh từ lâu rồi.", "I've had feelings for you for a long time." },
        { "Nếu anh xây xong dinh thự, chúng ta sẽ kết hôn!", "If you finish the mansion, we can get married!" },
        { "Em tin anh sẽ làm được. Yêu anh!", "I believe you can do it. Love you!" },
        { "Đêm nay ư? Anh có nghe tiếng lũ quỷ vào ban đêm chứ?", "Tonight? Have you heard the demons at night?" },
        { "Ở làng này, mỗi khi trời tối (18h \u2013 6h), lũ quỷ lại xuất hiện. Chúng phá hoại công trình và tấn công dân làng.", "In this village, whenever it gets dark (6 PM \u2013 6 AM), demons appear. They destroy buildings and attack the villagers." },
        { "Tràng Hạt là cách trừ tà tốt nhất \u2014 quả cầu thánh hạ gục kẻ thù chỉ một đòn.", "The Rosary is the best way to exorcise \u2014 a holy orb that takes down enemies in one hit." },
        { "Nhớ đóng cửa khi trời tối để cản bước chúng nhé. Em tặng anh chiếc tràng hạt này!", "Remember to close the door at night to keep them out. I'm giving you this rosary!" },
        { "Anh lại cần tràng hạt à? Em tặng anh thêm một chiếc nhé!", "You need another rosary? Here, take one more!" },
        { "Anh gọi em à?", "Did you call me?" },
        { "Em thích ở bên anh thế này.", "I love being by your side like this." },
        { "Ok con dê!", "Ok goat!" },
        { "Chồng yêu ơi! Hôm nay mình hạnh phúc lắm nhé.", "My dear husband! Let's be happy today." },
        { "Em luôn ở bên anh, dù nông trại có bận rộn đến đâu.", "I'll always be by your side, no matter how busy the farm gets." },
        { "Cảm ơn anh đã xây dựng dinh thự cho mình. Em yêu anh!", "Thank you for building our mansion. I love you!" },
        { "Anh ơi... dinh thự đã hoàn thành rồi!", "Honey... the mansion is complete!" },
        { "Em rất hạnh phúc. Em không ngờ anh làm được đến vậy.", "I'm so happy. I never thought you could pull it off." },
        { "Nếu anh muốn... mình có thể kết hôn. Em đồng ý!", "If you want... we can get married. I accept!" },
        { "Từ giờ, em sẽ mãi bên anh. Cảm ơn anh nhé!", "From now on, I'll be with you forever. Thank you!" },
        { "Chào anh! Em là Jessica, cô gái hàng xóm.", "Hi! I'm Jessica, your neighbor." },
        { "Nghe nói anh về nông thôn sống... Hy vọng mình sẽ là hàng xóm tốt nhé!", "I heard you moved back to the countryside... I hope we'll be good neighbors!" },
        { "Nhà em ở bên kia, anh cứ qua chơi bất cứ lúc nào.", "My house is over there, come visit anytime." },
        { "Chào anh! Hôm nay trông anh có vẻ tốt lắm.", "Hi! You look great today." },
        { "Em luôn ở đây nếu anh cần gì nhé.", "I'm always here if you need anything." },
        { "Anh nhớ câu 3 con cá giúp em nhé! Em đã tặng anh chiếc cần câu rồi đấy.", "Remember to catch 3 fish for me! I already gave you a fishing rod." },
        { "Lại đây anh ơi! Em có vài việc nhờ anh giúp.", "Come here! I have a few things to ask of you." },
        { "Giúp em xong em cảm ơn nhiều lắm!", "Help me out and I'll be so grateful!" },
        { "Chào anh! Hôm nay anh có khỏe không?", "Hi! How are you today?" },
        { "Em cảm ơn anh đã luôn quan tâm nhé!", "Thanks for always caring about me!" },
        { "Chào anh!", "Hi!" },
        { "Jessica tặng anh chiếc cần câu cá!", "Jessica gave you a fishing rod!" },
        { "Hoàn thành Câu Cá Lần Đầu! +10 độ thân mật", "Completed First Fishing Trip! +10 affection" },
        { "Hoàn thành Trừ Tà Giúp Làng! +10 độ thân mật, +1 Max Karma", "Completed Exorcise For The Village! +10 affection, +1 Max Karma" },
        { "Hoàn thành nhiệm vụ từ Jessica! +1 Max Karma", "Completed quest from Jessica! +1 Max Karma" },
        { "Độ Thân Mật", "Affection" },
        { "Chạm để tiếp tục", "Tap to continue" },
        { "Nhấn E để tiếp tục", "Press E to continue" },
        { "Chạm để đóng", "Tap to close" },
        { "Nhấn E để đóng", "Press E to close" },
        { "[Tỏ Tình] (Chạm)", "[Confess] (Tap)" },
        { "[Tỏ Tình] Nhấn T", "[Confess] Press T" },
        { "[Mời Về Nhà] (Chạm)", "[Invite Home] (Tap)" },
        { "[Mời Về Nhà] Nhấn G", "[Invite Home] Press G" },
        { "[Hỏi Về Đêm] (Chạm)", "[Ask About Night] (Tap)" },
        { "[Hỏi Về Đêm] Nhấn V", "[Ask About Night] Press V" },
        { "[Mở Cửa Hàng]", "[Open Shop]" },
        { "Bỏ Qua", "Skip" },
        { "Bỏ Qua [ESC]", "Skip [ESC]" },
        { "KẾT THÚC BUỒN", "SAD ENDING" },
        { "Bạn đã đến quá muộn.\nTrong khi bạn đi tìm kiếm giàu sang,\nbạn đã quên đi điều thực sự quan trọng.\n\nCô ấy đợi...\ncho đến khi không thể đợi nữa.",
          "You arrived too late.\nWhile you went chasing wealth,\nyou forgot what truly mattered.\n\nShe waited...\nuntil she could wait no more." },
        { "Tiếp tục cuộc phiêu lưu!", "Continue the adventure!" },
        { "KẾT THÚC HẠNH PHÚC", "HAPPY ENDING" },
        { "Bạn và Jessica đã đi đến cuối con đường cùng nhau!", "You and Jessica walked the path together to the very end!" },
        { "Nhấn Enter để tiếp tục chơi", "Press Enter to keep playing" },
        { "KẾT THÚC ĐỊNH MỆNH", "FATED ENDING" },
        { "Bạn và Jessica đã xây xong dinh thự... nhưng không bao giờ diệt Quỷ Vương,\nkhông lật tẩy bí mật của Phú Ông.\n\nMột đêm, kẻ nghiện ngập do ma túy của Phú Ông đã đột nhập.\nCảnh sát tìm thấy hai thi thể trong chính ngôi nhà bạn xây nên.\nDấu vết: một vụ trộm... do nghiện ngập.\n\nVà lũ quỷ vẫn đứng im ở rìa màn đêm,\nkhông một ai nhìn thấy chúng.\n\nĐịnh mệnh của bạn đã kết thúc ngay trong nhà mình.",
          "You and Jessica finished the mansion... but you never slew the Demon King,\nnever exposed the rich man's secret.\n\nOne night, an addict hooked on the rich man's drugs broke in.\nThe police found two bodies inside the very home you built.\nThe clue: a robbery... caused by addiction.\n\nAnd the demons still stood still at the edge of the night,\nand no one ever saw them.\n\nYour fate ended inside your own home." },
        { "Cửa dinh thự mở toang... còn chiếc xe cảnh sát đậu bên ngoài.", "The mansion door is wide open... and a police car is parked outside." },
        { "Trong phòng... hai thi thể nằm bất động.", "Inside the room... two bodies lie still." },
        { "Cửa bị phá. Đồ đạc vương vãi khắp nơi.", "The door was forced. Belongings are strewn everywhere." },
        { "Một vụ trộm... nhưng chỉ mất vài đồng vàng vụn.", "A robbery... but only a few loose coins are missing." },
        { "Khoan đã... bơm kim tiêm. Dấu vết nghiện ngập.", "Wait... a syringe. Signs of drug abuse." },
        { "Kẻ nghiện của đường dây Phú Ông đã vào đây.", "An addict from the rich man's drug ring got in." },
        { "Và lũ quỷ... vẫn đứng im ngay rìa bóng tối. Không ai nhìn thấy chúng.", "And the demons... still stand still at the edge of the darkness. No one sees them." },
        { "Bạn đã ngủ {0} tiếng.", "You slept for {0} hours." },
        { "Cần đến gần biển để câu cá!", "Get closer to the sea to fish!" },
        { "Ngắm xuống mặt nước!", "Aim at the water!" },
        { "Ngắm ra xa hơn về phía biển!", "Aim further out toward the sea!" },
        { "Cá thoát rồi!", "The fish got away!" },
        { "Bắt được {0}! Nó quẫy trên bờ — dùng gậy gõ cho xỉu!", "Caught a {0}! It's flopping on the shore — hit it with a club to knock it out!" },
        { "Bắt được {0}!", "Caught a {0}!" },
        { "Đã gõ cá xỉu! Nhặt lên thôi.", "Knocked the fish out! Pick it up." },
        { "Cá đã nằm im, nhặt lên thôi!", "The fish is still now, pick it up!" },
        { "Kéo Cá!", "Reel In!" },
        { "Bắt Được Cá!", "Got The Fish!" },
        { "Cá Thoát!", "Fish Escaped!" },
        { "Đang Kéo...", "Reeling..." },
        { "Goblin đã gieo hạt giống giúp bạn!", "The goblin planted seeds for you!" },
        { "DÙNG", "USE" },
        { "CHẠY", "SPRINT" },
        { "NHẢY", "JUMP" },
        { "XÂY", "BUILD" },
        { "XOAY", "ROTATE" },
        { "Giấc Ngủ", "Sleep" },
        { "Ngủ", "Sleep" },
        { "Ngồi", "Sit" },
        { "Đứng dậy", "Stand up" },
        { "Bạn đã đứng dậy.", "You have stood up." },
        { "Xem Cảnh Giao Dịch (Test)", "Watch Deal Scene (Test)" },
        { "Phú Ông đang ở quán bar... chờ đến trong đêm khuya.", "The rich man is at the club... wait until late at night." },
        { "Hủy", "Cancel" },
        { "Ngủ {0} tiếng", "Sleep {0} hours" },
        { "Hồi phục: +{0} Stamina / +{1} HP", "Restore: +{0} Stamina / +{1} HP" },
        { "Ruộng (đã thu hoạch — cày lại)", "Field (harvested — till again)" },
        { "{0} • Giai Đoạn {1}/4", "{0} • Stage {1}/4" },
        { "Ruộng đã cày — gieo hạt giống", "Field tilled — plant seeds" },
        { "Ruộng — dùng cuốc để cày", "Field — use a hoe to till" },
        { "1. Thu hoạch lúa 0/100\n2. Diệt quái 0/30\n3. Kiếm tiền 0/100000",
          "1. Harvest wheat 0/100\n2. Defeat monsters 0/30\n3. Earn money 0/100000" },
        { "Em đến rồi! Nhà anh thật ấm cúng.", "I'm here! Your house is so cozy." },
        { "Em có thể ở lại một lát không?", "Can I stay for a little while?" },
        { "Em chán quá!", "I'm so bored!" },
        { "Không đủ chỗ cho toàn bộ công trình.", "Not enough space for the whole building." },
        { "Không đủ gỗ/đá để sửa chữa.", "Not enough wood/stone to repair." },
        { "Đã sửa chữa xong.", "Repair complete." },

        // Random events
        { "Tất cả mùa màng của bạn đều tăng một giai đoạn!", "All your crops grow one stage!" },
        { "Tìm Thấy May Mắn", "Lucky Find" },
        { "Một đồng vàng xuất hiện trên mặt đất!", "A gold coin appears on the ground!" },
        { "Phục Hồi Thể Lực", "Stamina Restore" },
        { "Bạn cảm thấy sảng khoái!", "You feel refreshed!" },
        { "Suối Chữa Lành", "Healing Spring" },
        { "Vết thương của bạn đã lành!", "Your wounds have healed!" },
        { "Hạt Giống Miễn Phí", "Free Seeds" },
        { "Hạt giống rơi từ trên trời!", "Seeds fall from the sky!" },
        { "Sâu Bệnh Tấn Công", "Pest Invasion" },
        { "Sâu đang ăn mùa màng của bạn!", "Pests are eating your crops!" },
        { "Hạn Hán", "Drought" },
        { "Mặt trời làm khô hết ruộng của bạn!", "The sun dries out all your fields!" },
        { "Âm Thanh Kỳ Lạ", "Strange Noises" },
        { "Bạn nghe thấy âm thanh kỳ lạ ở gần...", "You hear strange noises nearby..." },
        { "Bệnh Mùa Màng", "Crop Disease" },
        { "Bệnh đang lây lan khắp mùa màng!", "Disease is spreading through your crops!" },
        { "Đom Đóm", "Fireflies" },
        { "Đom đóm nhảy múa xung quanh bạn!", "Fireflies dance around you!" },
        { "Cỏ Dại Mọc Lên", "Weeds Sprout" },
        { "Cỏ dại mọc um tùm trên mùa màng!", "Weeds overrun your crops!" },
        { "Hết Sức", "Exhausted" },
        { "Bạn cảm thấy kiệt sức!", "You feel exhausted!" },
        { "Thương Nhân Lang Thang", "Wandering Merchant" },
        { "Một thương nhân đã xuất hiện trên đường!", "A merchant appeared on the road!" },
        { "Cá Rơi", "Fish Rain" },
        { "Cá rơi từ bầu trời!", "Fish fall from the sky!" },
        { "Kẻ Thù Tấn Công", "Enemy Raid" },
        { "Kẻ thù đang tiến về phía bạn!", "Enemies are heading your way!" },
        { "Bão Gây Hại", "Storm Damage" },
        { "Bão phá hủy công trình của bạn!", "The storm destroys your buildings!" },
        { "Kẻ Trộm", "Thief" },
        { "Kẻ trộm lấy mất một phần tiền của bạn!", "A thief steals some of your money!" },
        { "Thị Trường Sụp Đổ", "Market Crash" },
        { "Thị trường sụp đổ! Giá bán giảm một nửa!", "The market crashed! Sell prices halved!" },
        { "Giá bán giảm một nửa trong 2 giờ!", "Sell prices halved for 2 hours!" },
        { "Giá Tăng Cao", "Price Surge" },
        { "Giá tăng vọt! Giá bán gấp đôi!", "Prices surge! Sell prices doubled!" },
        { "Giá bán gấp đôi trong 2 giờ!", "Sell prices doubled for 2 hours!" },
        { "Cầu Vồng", "Rainbow" },
        { "Một cầu vồng xuất hiện trên bầu trời!", "A rainbow appears in the sky!" },
        { "Động Vật Nhảy Múa", "Dancing Animals" },
        { "Động vật của bạn bắt đầu nhảy múa!", "Your animals start dancing!" },
        { "Tuyến Thương Mại", "Trade Route" },
        { "Tuyến thương mại mới mở! Giá mua giảm!", "A new trade route opened! Buy prices dropped!" },
        { "Giá mua giảm trong 1 ngày!", "Buy prices dropped for 1 day!" },
        { "Kẻ Thù Khổng Lồ", "Giant Enemy" },
        { "Một kẻ thù khổng lồ đã xuất hiện!", "A giant enemy has appeared!" },
        { "Đàn Tấn Công", "Swarm Attack" },
        { "Một đàn quái vật tấn công!", "A swarm of monsters attacks!" },
        { "Mưa Sao Băng", "Meteor Shower" },
        { "Sao băng rơi từ bầu trời!", "Meteors fall from the sky!" },
        { "Lễ Hội Thu Hoạch", "Harvest Festival" },
        { "Làng ăn mừng! Tiến độ nhiệm vụ tăng!", "The village celebrates! Quest progress increases!" },
        { "Pháo Hoa", "Fireworks" },
        { "Pháo hoa thắp sáng bầu trời!", "Fireworks light up the sky!" },
        { "Hình Ảnh Bóng Ma", "Ghostly Figures" },
        { "Bóng ma lang thang khắp đất...", "Ghosts roam the land..." },
        { "Bản Đồ Kho Báu", "Treasure Map" },
        { "Kho báu đã được chôn ở rìa bản đồ!", "A treasure is buried at the edge of the map!" },
        { "Rương kho báu đã xuất hiện ở rìa bản đồ!", "A treasure chest appeared at the edge of the map!" },
        { "Động Đất", "Earthquake" },
        { "Mặt đất rung chuyển dữ dội! Nhà cửa bị hư hại!", "The ground shakes violently! Buildings are damaged!" },
        { "Sấm Sét", "Lightning Storm" },
        { "Sét đánh xuống từ bầu trời!", "Lightning strikes from the sky!" },
        { "Lốc Xoáy", "Tornado" },
        { "Một cơn lốc xoáy quét qua thị trấn!", "A tornado sweeps through the town!" },

        // Tutorial pages
        { "CHÀO MỪNG!\n\nChào mừng đến với Country Life!\n\nSau khi tốt nghiệp ngành CNTT, thị trường việc làm đã quá khó khăn. Không có việc làm, bạn quay về nông thôn của ông nội đã khuất.",
          "WELCOME!\n\nWelcome to Country Life!\n\nAfter graduating in IT, the job market became too difficult. With no job, you return to your late grandfather's countryside." },
        { "BẮT ĐẦU CUỘC SỐNG MỚI\n\nTại đây, bạn phải xây dựng nông trại, bảo vệ làng, và tìm kiếm hạnh phúc cho mình.\n\nBiết đâu, cô gái hàng xóm sẽ là định mệnh của bạn...",
          "A NEW LIFE BEGINS\n\nHere, you must build a farm, protect the village, and find your own happiness.\n\nWho knows, the girl next door might be your destiny..." },
        { "DI CHUYỂN\n\nWASD \u2014 Di chuyển\nSpace \u2014 Nhảy\nShift \u2014 Chạy nhanh\nChuột \u2014 Nhìn xung quanh",
          "MOVEMENT\n\nWASD \u2014 Move\nSpace \u2014 Jump\nShift \u2014 Run faster\nMouse \u2014 Look around" },
        { "HÀNH ĐỘNG\n\nChuột trái \u2014 Sử dụng công cụ\nE \u2014 Tương tác / Mở cửa\nQ \u2014 Bỏ vật phẩm",
          "ACTIONS\n\nLeft mouse \u2014 Use tool\nE \u2014 Interact / Open door\nQ \u2014 Drop item" },
        { "XÂY DỰNG\n\nGiữ Búa + F \u2014 Mở menu xây dựng\nB / N \u2014 Đổi loại công trình\nChuột trái \u2014 Đặt công trình\nF \u2014 Hủy",
          "BUILDING\n\nHold Hammer + F \u2014 Open build menu\nB / N \u2014 Switch building type\nLeft mouse \u2014 Place building\nF \u2014 Cancel" },
        { "NÔNG NGHIỆP\n\nCuốc \u2014 Làm đất để trồng cây\nLưỡi liềm \u2014 Thu hoạch\nRìu / Cuốc chim \u2014 Thu thập nguyên liệu",
          "FARMING\n\nHoe \u2014 Till soil to plant crops\nSickle \u2014 Harvest\nAxe / Pickaxe \u2014 Gather materials" },
        { "KẺ THÙ\n\nBan đêm (18h \u2013 6h), lũ quỷ xuất hiện và tấn công bạn cùng các công trình.\nQuỷ thường: 50 máu, gây 10 sát thương.\nQuỷ khổng lồ: máu và sát thương cao hơn.",
          "ENEMIES\n\nAt night (6 PM \u2013 6 AM), demons appear and attack you and your buildings.\nNormal demon: 50 HP, deals 10 damage.\nGiant demon: higher HP and damage." },
        { "TRỪ TÀ\n\nTràng hạt \u2014 Quả cầu thánh hạ gục một đòn.\nTrang bị Tràng Hạt rồi bấm chuột trái để thi triển.\n\nHãy đóng cửa khi trời tối để cản bước chúng!",
          "HOLY MAGIC\n\nRosary \u2014 A holy orb that takes down enemies in one hit.\nEquip the Rosary and press left mouse to cast.\n\nClose the door at night to stop them!" },
        { "NGÔI CHÙA\n\nNgôi chùa 4 tầng mái cong nằm phía Đông làng, ngay cạnh nhà bà hàng xóm.\n\nĐây là công trình biểu tượng của làng \u2014 hãy đến chiêm bái và ngắm cảnh hoàng hôn từ nơi đây.",
          "THE PAGODA\n\nThe 4-tier curved-roof pagoda lies east of the village, right next to the neighbor's house.\n\nIt is the village's landmark \u2014 come pay your respects and watch the sunset from there." },
        { "MẸO\n\nThu hoạch lúa để kiếm tiền\nXây dựng tường và tháp canh để bảo vệ\nHoàn thành nhiệm vụ để nhận thưởng\nNgủ trên giường để lưu game",
          "TIPS\n\nHarvest wheat to earn money\nBuild walls and watchtowers for protection\nComplete quests to earn rewards\nSleep on the bed to save the game" },
        { "CÂU CÁ\n\nTrò chơi nhỏ \u2014 Câu cá!\n\nTrang bị Cần Câu (quà của Jessica), đứng gần biển phía Tây, nhắm ra mặt nước và bấm chuột trái để thả lưỡi câu.\n\nChờ bóng cá bơi tới phao \u2014 khi phao rung, bấm chuột trái để bắt đầu kéo.",
          "FISHING\n\nMinigame \u2014 Fishing!\n\nEquip the Fishing Rod (a gift from Jessica), stand by the sea to the west, aim at the water and left-click to cast the hook.\n\nWait for the fish shadow to reach the bobber \u2014 when the hook shakes, left-click to start reeling." },
        { "CÂU CÁ (TIẾP)\n\nKéo vòng tròn giữa màn hình để di chuyển vạch trắng \u2014 giữ nó trong vùng xanh để lấp đầy thanh tiến độ.\n\nCá có thể quẫy trên bờ \u2014 dùng Gậy gõ cho xỉu rồi nhặt lên.\n\nCá bán được tiền: Chép 15, Hồi 25, Ngừ 40, Nóc 60.",
          "FISHING (CONTINUED)\n\nDrag the wheel in the middle of the screen to move the white line \u2014 keep it inside the green zone to fill the progress bar.\n\nFish can flop on the shore \u2014 hit them with the Club to knock them out, then pick them up.\n\nFish sell for: Carp 15, Salmon 25, Tuna 40, Pufferfish 60." },

        // Item names (VN source shown to player in Vietnamese mode)
        { "Búa", "Hammer" },
        { "Lưỡi Hái", "Scythe" },
        { "Cây Gậy", "Club" },
        { "Đậu Bắn", "Peashooter" },
        { "Đuốc", "Torch" },
        { "Nông Sản", "Crops" },
        { "Gỗ", "Wood" },
        { "Đá", "Stone" },
        { "Trứng", "Egg" },
        { "Sữa", "Milk" },
        { "Len", "Wool" },
        { "Thịt", "Meat" },
        { "Lông Vũ", "Feather" },
        { "Mật Ong", "Honey" },
        { "Xôi Gấc", "Red Sticky Rice" },
        { "Súp Bí Ngòi", "Pumpkin Soup" },
        { "Mứt Cà Rốt", "Carrot Jam" },
        { "Trái Cây Khô", "Dried Fruit" },
        { "Dưa Chua", "Pickles" },
        { "Rượu Gạo", "Rice Wine" },
        { "Tương Ớt", "Chili Sauce" },
        { "Rượu Thuốc", "Tonic Wine" },
        { "Tinh Dược", "Elixir" },
        { "Sừng Quỷ", "Demon Horn" },
        { "Tinh Chất Bóng Tối", "Dark Essence" },
        { "Xương Quái Vật", "Monster Bone" },
        { "Bếp Nấu", "Cooking Stove" },
        { "Lọ Ngâm", "Preserve Jar" },
        { "Nồi Ủ", "Brewing Kettle" },
        { "Hạt Giống Lúa Mì", "Wheat Seed" },
        { "Hạt Giống Ngô", "Corn Seed" },
        { "Hạt Giống Đậu", "Peashooter Seed" },
        { "Hạt Giống Khoai Tây", "Potato Seed" },
        { "Hạt Giống Cà Rốt", "Carrot Seed" },
        { "Hạt Giống Cà Chua", "Tomato Seed" },
        { "Hạt Giống Dâu Tây", "Strawberry Seed" },
        { "Hạt Giống Bí Ngòi", "Pumpkin Seed" },
        { "Hạt Giống Hành Tây", "Onion Seed" },
        { "Hạt Giống Mía", "Sugarcane Seed" },
        { "Hạt Giống Gạo", "Rice Seed" },

        // Building names
        { "Tường Gỗ", "Wood Wall" },
        { "Tường Đá", "Stone Wall" },
        { "Hàng Rào", "Fence" },
        { "Lính Canh", "Watchtower" },
        { "Nhà Nhỏ", "Small House" },
        { "Sàn Gỗ", "Wood Floor" },
        { "Sàn Đá", "Stone Floor" },
        { "Cầu Thang", "Staircase" },
        { "Bàn", "Table" },
        { "Ghế", "Chair" },
        { "Ghế Sofa", "Sofa" },
        { "Cửa", "Door" },
        { "Nhà Vợ", "Wife's House" },
        { "Nhà Cấu Trúc", "Structure House" },
        { "Túp Lều Goblin", "Goblin Hut" },

        // Mansion part names
        { "Nền", "Foundation" },
        { "Sân Trước", "Front Yard" },
        { "Sân Sau", "Back Patio" },
        { "Sàn Tầng 1", "1F Floor" },
        { "Tường Bên Ngoài T1", "1F Exterior Walls" },
        { "Tường Bên Trong T1", "1F Interior Walls" },
        { "Cửa Chính", "Front Door" },
        { "Phòng Khách", "Living Room" },
        { "Nhà Bếp", "Kitchen" },
        { "Phòng Ăn", "Dining Room" },
        { "Phòng Tắm T1", "1F Bathroom" },
        { "Sàn Tầng 2", "2F Floor" },
        { "Tường Bên Ngoài T2", "2F Exterior Walls" },
        { "Tường Bên Trong T2", "2F Interior Walls" },
        { "Phòng Ngủ Chính", "Master Bedroom" },
        { "Phòng Ngủ 2", "Bedroom 2" },
        { "Phòng Ngủ 3", "Bedroom 3" },
        { "Phòng Tắm T2", "2F Bathroom" },
        { "Hành Lang", "Hallway" },
        { "Mái Chính", "Main Roof" },
        { "Mái Sân Trước", "Porch Roof" },
        { "Ban Công", "Balcony" },
        { "Lối Vào", "Garden Path" },

        // Buffalo dialog
        { "Chào bạn! Tôi là Buffalo, chủ cửa hàng của làng.", "Hi there! I'm Buffalo, the village shopkeeper." },
        { "Tôi bán hạt giống, công cụ và thức ăn cho gia súc.", "I sell seeds, tools, and livestock feed." },
        { "Ghế cửa hàng bất cứ khi nào bạn cần nhé!", "Drop by the shop whenever you need anything!" },

        // Restaurant
        { "Nhà Hàng", "Restaurant" },
        { "Đầu Bếp", "Chef" },
        { "Cửa Hàng Câu Cá", "Fishing Shop" },
        { "Người Bán Câu Cá", "Fishing Vendor" },
        { "Cơm Trắng", "Steamed Rice" },
        { "Cơm Tấm", "Broken Rice" },
        { "Cơm Gà", "Chicken Rice" },
        { "Cơm Chiên", "Fried Rice" },
        { "Món cơm nóng hổi đây, ai vào ăn nhanh nào!", "Hot rice dishes ready, come and eat!" },
        { "Chào anh! Cần câu hay mồi gì không?", "Hey there! Need a rod or some bait?" },
        { "Cá ở đây nhiều lắm, chỉ cần kiên nhẫn thôi.", "There are plenty of fish here, just be patient." },
        { "Cần câu của ta đều làm từ tre già, bền lắm đấy!", "Our rods are made from old bamboo, very durable!" },
        { "Cứ chọn đi, đảm bảo giá rẻ hơn ngoài bờ sông.", "Go ahead and pick, guaranteed cheaper than by the river." },
        { "Nhấn E để xem hàng", "Press E to browse goods" },
        { "Cơm trắng cho người cần nạp năng lượng nhẹ.", "Steamed rice for a light energy boost." },
        { "Ăn no xong là cày ruộng khỏe lại ngay!", "Eat up and you'll be farming again in no time!" },
        { "Đã ăn {0}. Hồi phục +{1} Thể Lực, +{2} Máu!", "Ate {0}. Restored +{1} Stamina, +{2} HP!" },
        { "Đã dùng {0}. +{1} Máu!", "Used {0}. +{1} HP!" },
        { "Ruộng đã lớn nhanh hơn!", "The field grew faster!" },

        // Pagoda monk
        { "Nhà Sư", "Monk" },
        { "A di đà Phật. Con đến chùa lễ Phật à?", "Amitabha. Have you come to pray?" },
        { "Ngôi chùa này là cột mốc của làng, con hãy trân trọng nó.", "This pagoda is the village's landmark — treasure it." },
        { "Muốn khỏe khoắn cả ngày, hãy ăn uống đầy đủ rồi mới ra đồng.", "To stay strong all day, eat well before heading to the fields." },
        { "Ngủ đủ giấc cũng là một cách dưỡng sức, con đừng quên.", "Plenty of sleep also restores your strength — don't forget." },
        { "Câu cá hay thu hoạch đều cần sức. Chú ý giữ gìn sức khỏe.", "Fishing and harvesting both take energy. Take care of yourself." },
        { "Bình tâm. Làng này còn lắm chuyện phải trải qua.", "Stay calm. There is much this village has yet to go through." },
        { "Người nông dân giỏi là người biết tiết kiệm sức lực.", "A good farmer knows how to conserve their strength." },
        { "Mỗi ngày dâng một bát gạo, nhà chùa sẽ phù hộ con khỏe mạnh.", "Offer a bowl of rice each day and the pagoda will grant you good health." },
        { "Ruộng lúa tốt nhờ nước, con người khỏe nhờ điều độ.", "Good rice paddies need water; strong people need moderation." },
        { "Con có gạo không? Dâng cho nhà chùa một bát gạo, ta sẽ ban phước lành sức khỏe cho con cả ngày hôm nay.", "Do you have rice? Offer the pagoda a bowl of rice and I will bless your health for the whole day." },
        { "Con đã nhận phước lành hôm nay rồi. Hãy quay lại vào ngày mai nếu muốn dâng gạo tiếp.", "You have already received today's blessing. Return tomorrow if you wish to offer more rice." },
        { "Con thành tâm dâng gạo, nhà chùa xin ban phước lành. Sức lực của con sẽ hồi phục nhanh gấp đôi cả ngày hôm nay.", "You offer rice with a sincere heart. I bless you — your stamina will recover twice as fast for the rest of the day." },
        { "Con chưa mang gạo theo người. Hãy quay lại khi có gạo nhé.", "You have no rice with you. Come back when you have some." },
        { "Phước lành: hồi phục sức lực gấp đôi cả ngày!", "Blessing: stamina recovers twice as fast all day!" },
        { "Nhấn E để dâng gạo", "Press E to offer rice" },
        { "Chạm để dâng gạo", "Tap to offer rice" },

        // Karma & Meditation
        { "Con đã nhận phước lành hôm nay rồi.", "You have already received today's blessing." },
        { "Con có muốn thiền định để gia tăng giới hạn phước đức không?", "Would you like to meditate to increase your max karma?" },
        { "Thiền Định", "Meditate" },
        { "Nhấn E để thiền định", "Press E to meditate" },
        { "Chạm để thiền định", "Tap to meditate" },
        { "Thanh tinh! +1 Max Karma", "Pure! +1 Max Karma" },
        { "Hay thu lai lan sau!", "Try again next time!" },
        { "Hết phước đức!", "Out of karma!" },
        { "Phước Đức", "Karma" },
        { "Thien Tung", "Meditation" },
        { "Go doan van ben duoi...", "Type the paragraph below..." },
        { "ESC de dung | Backspace de xoa", "ESC to stop | Backspace to delete" },

        // Skills & XP
        { "Cấp độ Canh Tác lên {0}! Bạn thu hoạch và hồi sức hiệu quả hơn.", "Farming reached level {0}! You harvest and use stamina more efficiently." },
        { "Cấp độ Câu Cá lên {0}! Cá khó thoát và kéo nhanh hơn.", "Fishing reached level {0}! Fish escape less and you reel faster." },
        { "Năng suất! +1 {0} nhờ kỹ năng Canh Tác.", "Bonus yield! +1 {0} from farming skill." },
        { "Mẻ kép! +1 {0} nhờ kỹ năng Câu Cá.", "Double catch! +1 {0} from fishing skill." },
        { "Kỹ Năng", "Skills" },
        { "Canh Tác", "Farming" },
        { "Câu Cá", "Fishing" },
        { "Cấp {0}", "Lv {0}" },
        { "Năng suất: 25% cơ hội +1 nông sản", "Yield: 25% chance of +1 crop" },
        { "Tiết kiệm sức: toàn bộ dụng cụ -25% thể lực", "Efficient: all tools cost -25% stamina" },
        { "Mẻ kép: 20% cơ hội bắt đôi", "Double catch: 20% chance of +1 fish" },
        { "Kéo nhanh: +15% tốc độ cuốn cá", "Reeling: +15% reel speed" },
        { "TỐI ĐA", "MAX" },
        { "Đạt cấp 2 và 4 để mở kỹ năng đặc biệt.", "Reach levels 2 and 4 to unlock special perks." },

        // Functional Buildings (Phase 2B)
        { "Tháp canh làm chậm kẻ thù gần đó!", "The watchtower slows nearby enemies!" },

        // NPC Friendship (Phase 2C)
        { "Tình Bạn", "Friendship" },
        { "Tình bạn lên {0} tim với {1}!", "Friendship reached {0} with {1}!" },
        { "Tình bạn đạt 5 tim với {0}!", "Friendship reached 5 hearts with {0}!" },
        { "Tình bạn lên 3 tim với {0}!", "Friendship reached 3 hearts with {0}!" },
        { "Nhà Sư tặng con một túi gạo vì đã thân thiết!", "The Monk gifts you a bag of rice for your friendship!" },
        { "Thủ Thư tặng con một ly cà phê sách!", "The Librarian gifts you a bookish coffee!" },
        { "Đầu Bếp tặng con một phần Cơm Gà ngon nhất quán!", "The Chef gifts you the best chicken rice in the restaurant!" },
        { "Người bán cá tặng con 2 mồi câu vì tin nhau!", "The Fish Shopkeeper gifts you 2 baits for trust!" },
        { "Tặng {0} cho {1}. Họ rất thích!", "Gave {0} to {1}. They loved it!" },
        { "Tặng {0} cho {1}.", "Gave {0} to {1}." },
        { "Không thể tặng món này ngay bây giờ.", "Can't give this item right now." },
        { "Tặng quà cho", "Giving gift to" },
        { "Nhấn số để tặng, G để đóng, Space để đổi trang", "Press a number to give, G to close, Space to switch page" },
        { "Túi đồ không có món quà nào.", "No gift items in your inventory." },

        // Fishing rod upgrades (Phase 3A)
        { "Cần Câu đã nâng lên Cấp {0}!", "Fishing rod upgraded to Level {0}!" },
        { "Nâng cấp này yêu cầu cần câu Cấp {0} trước.", "This upgrade requires the rod at Level {0} first." },
        { "Cần câu đã ở cấp tối đa của bậc này.", "The rod is already at the max of this tier." },
        { "Bạn cần nâng cấp cần câu đúng thứ tự.", "You must upgrade the rod in order." },

        // Crop quality (Phase 3B)
        { "Chất Lượng Tuyệt", "Great Quality" },
        { "Chất Lượng Tốt", "Good Quality" },
        { "Chất lượng Tuyệt! +1 {0} nông sản.", "Great quality! +1 {0} crop." },
        { "Chất lượng Tốt! +1 {0} nông sản.", "Good quality! +1 {0} crop." },

        // Chest storage (Phase 3C)
        { "Đã sắp xếp túi đồ.", "Inventory sorted & stacked." },
        { "Rương Đồ", "Chest" },
        { "Rương", "Chest" },
        { "Túi", "Bag" },
        { "Túi đồ", "Your bag" },
        { "Lấy", "Take" },
        { "Cất", "Store" },
        { "Rương đầy.", "The chest is full." },
        { "Đã lấy {0} từ rương.", "Took {0} from the chest." },
        { "Đã cất {0} vào rương.", "Stored {0} into the chest." },
        { "Trong rương: {0}/{1} loại — {2}", "In the chest: {0}/{1} types - {2}" },
        { "Chọn Lấy hoặc Cất", "Choose Take or Store" },

        // Event Test Panel
        { "Sự Kiện", "Events" },
        { "SỰ KIỆN TEST", "EVENT TEST" },
        { "Tier 0 — Cơ Bản", "Tier 0 — Basic" },
        { "Tier 1 — Nâng Cao", "Tier 1 — Advanced" },
        { "Tier 2 — Quý Hiếm", "Tier 2 — Rare" },

        // Animal names
        { "Bò", "Cow" },
        { "Lợn", "Pig" },
        { "Cừu", "Sheep" },
        { "Dê", "Goat" },
        { "Gà", "Chicken" },
        { "Vịt", "Duck" },
        { "Gà Tây", "Turkey" },

        // Rich man NPC
        { "Phú Ông", "The Rich Man" },
        { "Jessica của cậu dạo này trông cô đơn lắm đấy.", "That Jessica of yours looks awfully lonely these days." },
        { "Nếu cậu cứ mải làm nông, tôi sẽ đưa cô ấy đi cho xem.", "If you keep burying yourself in farm work, I'll take her away." },
        { "Tôi giàu có, còn cậu thì sao? Cô ấy xứng đáng cuộc sống tốt hơn.", "I'm rich, and you? She deserves a better life." },
        { "Ông chú giàu có lại sang nhà Jessica giữa đêm...", "The rich man walked over to Jessica's house in the middle of the night..." },
        { "Jessica: Anh dạo này bận quá... em nhớ anh.", "Jessica: You've been so busy... I miss you." },
        { "Jessica: Em nghe nói ông chú giàu có kia cứ quanh quẩn gần nhà...", "Jessica: I hear that rich man keeps hanging around the house..." },
        { "Jessica: Anh không còn quan tâm em nữa sao? Ông ta đã ngỏ lời mời em đi...", "Jessica: Don't you care about me anymore? He's already asked me to leave with him..." },

        // NTR ending
        { "KẾT THÚC NTR", "NTR ENDING" },
        { "Bạn đã bỏ bê Jessica quá lâu.\nÔng chú giàu có đã lặng lẽ lấp đầy khoảng trống bạn để lại.\n\nKhi bạn quay lại... cô ấy đã không còn chờ đợi bạn nữa.\nBạn đã quá muộn.\n\nKhi bạn không quan tâm đến cô ấy,\nngười khác sẽ quan tâm thay bạn.", "You neglected Jessica for too long.\nThe rich man quietly filled the void you left.\n\nWhen you came back... she no longer waited for you.\nYou were too late.\n\nWhen you fail to care for her,\nsomeone else will." },

        // Rich man illegal trade
        { "Hừ. Một kẻ làm ruộng như cậu mà cũng dám bắt chuyện với ta?", "Hmph. A farmhand like you dares to start a conversation with me?" },
        { "Ta có vàng, có đất, cả nửa dãy phố. Còn cậu? Một mảnh ruộng và vài con gà.", "I have gold, land, half the street. And you? A little field and a few chickens." },
        { "Đừng làm ta mất thời gian. Về lo đám lúa của cậu đi.", "Don't waste my time. Go tend your rice." },
        { "Cậu... đã nhìn thấy chuyện đêm qua rồi sao?", "You... saw last night's business, didn't you?" },
        { "Được thôi. Cậu là người thông minh. Chúng ta có thể... thỏa thuận.", "Fine. You're clever. We can... make a deal." },
        { "Im lặng, và cậu sẽ có một món tiền cậu không thể từ chối.", "Stay quiet, and you'll get a sum you can't refuse." },
        { "[Bỏ Đi] (Chạm)", "[Walk Away] (Tap)" },
        { "[Bỏ Đi] Ấn 1", "[Walk Away] Press 1" },
        { "[Nhận Hối Lộ] (Chạm)", "[Take Bribe] (Tap)" },
        { "[Nhận Hối Lộ] Ấn 2", "[Take Bribe] Press 2" },
        { "Hãy lựa chọn...", "Make your choice..." },
        { "Lựa chọn đặc biệt", "Special Choice" },
        { "Bạn đã phát hiện hoạt động phi pháp của Phú Ông!", "You discovered the rich man's illegal activities!" },
        { "Bí Mật Của Phú Ông", "The Rich Man's Secret" },
        { "Đêm tối, hãy rình xem điều gì xảy ra sau dinh thự của Phú Ông. Sau khi có bằng chứng, hãy đến đồn cảnh sát bên cạnh con đường để báo án.", "At night, spy on what happens behind the rich man's mansion. Once you have proof, report it to the police post by the road." },

        // Police post / officer
        { "Cảnh Sát", "Police Officer" },
        { "Chào cậu. Công việc của tôi là giữ bình yên cho thôn này.", "Hello. My job is to keep this village at peace." },
        { "Nghe nói đêm đêm quanh dinh thự Phú Ông có kẻ lạ ra vào bí mật...", "I hear strangers come and go around the rich man's mansion at night..." },
        { "Nếu cậu thấy gì bất thường, hãy đến báo ngay cho đồn.", "If you see anything unusual, come report it to the station right away." },
        { "Cậu tới đúng lúc. Chúng tôi đã nghi ngờ hắn từ lâu.", "You came just in time. We've suspected him for a long time." },
        { "Những giao dịch ban đêm của hắn không lọt khỏi mắt chúng tôi.", "His nightly deals haven't escaped our eyes." },
        { "Cảm ơn cậu. Đồng chí, vào việc thôi!", "Thank you. Comrades, let's get to work!" },

        // Justice ending (rich man resolved before the Demon King is slain)
        { "CÔNG LÝ ĐƯỢC THỰC THI NHƯNG HIỂM HỌA CHƯA QUA", "JUSTICE SERVED BUT NOT ALL THREAT IS GONE" },
        { "Cậu đã lật tẩy bộ mặt thật của Phú Ông.\nCảnh sát đã đến, và hắn bị bắt ngay trước dinh thự của chính mình.\n\nĐêm ấy, cậu và Jessica trở về nhà, ngủ say.\nGiữa đêm, cô chợt mở mắt...\nmột con quỷ đang nhìn cô chằm chằm.\n\nSáng hôm sau... Jessica đã biến mất.\nCảnh sát kéo đến điều tra căn nhà, nhưng không tìm được dấu vết nào.\n\nCậu chạy lên chùa tìm thầy. Thầy trầm ngâm:\n\"Jessica không bị người bắt... thứ bước vào đêm ấy là quỷ.\nHãy tìm cô ấy trước khi màn đêm buông xuống.\"\nHiểm họa thật sự vẫn chưa qua.", "You exposed the rich man's true face.\nThe police arrived, and he was arrested in front of his own mansion.\n\nThat night, you and Jessica went home and fell asleep.\nIn the middle of the night, she opened her eyes...\na demon was staring at her.\n\nThe next morning... Jessica had vanished.\nThe police came to investigate the house, but found no trace.\n\nYou ran to the pagoda to find the monk. He sighed:\n\"Jessica was not taken by men... what came in the night was a demon.\nFind her before the sun sets.\"\nThe true threat is not yet gone." },

        // Corrupted ending (bribe)
        { "KẾT THÚC ĐỒI BẠI", "CORRUPTED ENDING" },
        { "Cậu đã im lặng. Và cậu đã được trả một cái giá rất hậu hĩnh.\n\nNhưng đêm xuống, những chiếc xe vẫn nối đuôi nhau đến dinh thự.\nJessica vẫn đang trong tầm ngắm của hắn...\n\nVà giờ, cậu là một phần của câu chuyện đó.", "You stayed silent. And you were paid handsomely.\n\nBut when night falls, the cars still line up at the mansion.\nJessica is still in his sights...\n\nAnd now, you are part of that story." },

        // Monk quest chain — cleansing the monastery
        { "Trừ Tà Quanh Chùa", "Cleanse The Monastery" },
        { "Dùng Tràng Hạt tiêu diệt 5 con quỷ quanh chùa để bảo vệ làng.", "Use the Rosary to defeat 5 demons around the monastery to protect the village." },
        { "Lũ quỷ nhỏ đang quấy phá quanh chùa. Dùng Tràng Hạt tiêu diệt 5 con để chúng khiếp sợ!", "Small demons are stirring around the monastery. Use the Rosary to defeat 5 of them and strike fear into their hearts!" },
        { "Con hãy tiếp tục dùng Tràng Hạt. Tiến độ: {0}/5", "Keep using the Rosary. Progress: {0}/5" },

        // Monk quest chain — the Demon King
        { "Trấn Áp Quỷ Vương", "Vanquish The Demon King" },
        { "Quỷ Vương đã thức tỉnh ở cuối con đường phía đông. Dùng Tràng Hạt tiêu diệt nó để bảo vệ làng!", "The Demon King has awakened at the end of the eastern road. Use the Rosary to destroy it and protect the village!" },
        { "Ta thấy con đã dùng Tràng Hạt trừ được quỷ dữ. Giờ ta sẽ khai mở Linh Nhãn cho con.", "I see you have used the Rosary to banish demons. Now I will open your spiritual eye." },
        { "Con sẽ thấy những thứ người thường không thấy. Hãy nhìn về cuối con đường phía đông... Quỷ Vương đã thức tỉnh!", "You will see what ordinary eyes cannot. Look toward the end of the eastern road... The Demon King has awakened!" },
        { "Quỷ Vương đang ngự trị ở cuối con đường phía đông. Hãy dùng Tràng Hạt để trừ tà!", "The Demon King reigns at the end of the eastern road. Use the Rosary to banish it!" },
        { "Con đã trấn áp Quỷ Vương. Làng này nợ con một ân tình lớn, Phật sẽ phù hộ con.", "You have vanquished the Demon King. The village owes you a great debt; Buddha will bless you." },
        { "QUỶ VƯƠNG ĐÃ THỨC TỈNH!", "THE DEMON KING HAS AWAKENED!" },
        { "Quỷ Vương", "Demon King" },

        // Immigrant quest chain — building homes for the newcomers
        { "Người Di Cư", "Immigrant" },
        { "Xây Nhà Cho Người Di Cư", "Build Homes for the Immigrants" },
        { "Xây 3 ngôi nhà nhỏ cho những người di cư ở phía nam con đường.", "Build 3 small houses for the immigrants south of the road." },
        { "Người di cư đã đến! Hãy xây nhà cho họ!", "The immigrants have arrived! Build homes for them!" },
        { "Blueprint đã được đặt tại vị trí quy hoạch! Hãy thu thập gỗ & đá.", "Blueprint placed at the designated plot! Gather wood & stone." },
        { "Nhận {0} đồng tiền thuê nhà từ khu người di cư!", "Received {0} coins in rent from the immigrant village!" },
        { "Người mới à? Chúng tôi vừa rời làng cũ, nơi ấy đã chẳng còn chỗ cho chúng tôi nữa.", "Newcomer? We just left our old village; there was no place left for us there." },
        { "Nghe nói vùng đất này còn nhiều chỗ trống. Ông chủ giúp chúng tôi một việc được không?", "We heard this land still has room. Boss, can you help us?" },
        { "Xin hãy dựng 3 căn nhà nhỏ cho chúng tôi. Chúng tôi chỉ cần một mái che thôi.", "Please build us 3 small houses. We only need a roof over our heads." },
        { "Cảm ơn ông chủ! Chúng tôi vẫn cần thêm {0} căn nhà nữa.", "Thank you, boss! We still need {0} more houses." },
        { "Chúng tôi vẫn cần {0} căn nhà nữa. Ông chủ cố gắng giúp nhé.", "We still need {0} more houses. Please do your best, boss." },
        { "Ông chủ đã cho chúng tôi một cuộc sống mới! Mỗi sáng chúng tôi sẽ trả tiền thuê nhà cho ông chủ.", "Boss, you gave us a new life! Every morning we will pay you rent." },
        { "Từ nay khu vực này là một phần của làng. Chúng tôi sẽ luôn biết ơn ông chủ.", "From now on this area is part of the village. We will always be grateful to you, boss." },
        { "Gọi Người Di Cư", "Call the Immigrant" },
        { "Một gia đình người di cư đang đến làng!", "A family of immigrants is coming to town!" },
        { "Người di cư đã đến làng! Hãy đến chào họ.", "The immigrants have arrived! Go greet them." },

        // Immigrant one-at-a-time dialog
        { "Cảm ơn ông chủ! Gia đình tôi đã chuyển vào nhà mới.", "Thank you, boss! My family has moved into the new house." },
        { "Ông chủ cứ yên tâm, chúng tôi sẽ chăm chỉ làm việc.", "Don't worry, boss. We will work hard." },
        { "Khi nào có người di cư khác đến, ông chủ giúp họ nhé!", "When other immigrants come, please help them too, boss!" },
        { "Ngôi nhà của tôi vẫn chưa xong. Ông chủ giúp tôi dựng nhà nhé!", "My house is not done yet. Please help me build it, boss!" },
        { "Cảm ơn ông chủ! Nhà tôi sắp xong rồi.", "Thank you, boss! My house is almost done." },
        { "Xin chào ông chủ! Tôi vừa rời làng cũ, nơi ấy đã chẳng còn chỗ cho chúng tôi nữa.", "Hello boss! I just left my old village; there was no place left for us there." },
        { "Nghe nói vùng đất này còn nhiều chỗ trống. Ông chủ giúp tôi một việc được không?", "I heard this land still has room. Boss, can you help me?" },
        { "Xin hãy dựng một căn nhà nhỏ cho gia đình tôi. Chúng tôi chỉ cần một mái che thôi.", "Please build a small house for my family. We only need a roof over our heads." },
        { "Xây một ngôi nhà nhỏ cho người di cư ({0}/{1}).", "Build a small house for immigrants ({0}/{1})." },

        // Boss bad ending (fall into the dark)
        { "Tải Save Gần Nhất", "Load Latest Save" },
        { "RƠI VÀO BÓNG TỐI", "FALL INTO THE DARK" },
        { "Quỷ Vương đã quật ngã con.\nBóng tối nuốt chửng ngôi làng.\n\nSố phận của con dừng lại tại đây...\nHãy quay về nơi lưu gần nhất và đối mặt với nó lần nữa.", "The Demon King has struck you down.\nDarkness devours the village.\n\nYour fate ends here...\nReturn to the latest save and face it once more." },

        // Demon King slain but evil still lurks
        { "QUỶ VƯƠNG ĐÃ CHẾT NHƯNG CÁI ÁC CHƯA HẾT", "THE DEMON IS DEAD BUT NOT ALL EVIL IS GONE" },
        { "Quỷ Vương đã bị đánh bại, bóng tối bị đẩy lùi.\nNhưng khi cậu quay về làng...\nJessica đã bị một kẻ nghiện ngập do ma túy của Phú Ông hạ sát.\n\nKẻ gây án chỉ là bề nổi...\nCó thể đây là mưu đồ của lũ quỷ.\nCái ác chưa bị nhổ tận gốc.\nNgôi làng chưa thể yên bình.", "The Demon King is defeated, the darkness pushed back.\nBut when you returned to the village...\nJessica had been killed by a drug addict fed by the rich man's poison.\n\nThe killer is only the surface...\nThis may be the demons' doing.\nEvil has not been uprooted.\nThe village cannot rest easy." },

        // Demon ending captions
        { "Jessica đã bị hạ sát ngay trước hiên nhà.", "Jessica was killed right on her doorstep." },
        { "Không phải tôi... tôi không kiểm soát được nữa...", "It wasn't me... I couldn't control myself anymore..." },
        { "Cảnh sát nhanh chóng có mặt.", "The police arrive quickly." },
        { "Họ bắt giữ kẻ nghiện ngập... nhưng kẻ gây án chỉ là bề nổi.", "They arrest the addict... but the killer is only the surface." },

        // NPC dialogs, shop prompts, mobile buttons, quest tips
        { "Chạm để xem thực đơn", "Tap to view the menu" },
        { "Nhấn E để xem thực đơn", "Press E to view the menu" },
        { "Nhân Viên Quán Cà Phê", "Cafe Staff" },
        { "Trong lúc cậu mải làm nông, ông chú giàu có đã lặng lẽ đến gần cô ấy.\n\nKhi cậu quay lại...\nJessica đã không còn đợi cậu nữa.\n\nCậu đã để cô ấy ra đi, mãi mãi.", "While you were busy farming, the rich man quietly moved closer to her.\n\nWhen you looked back...\nJessica no longer waited for you.\n\nYou let her go, forever." },
        { "CẦU HÔN", "PROPOSE" },
        { "MỜI", "INVITE" },
        { "NGỦ", "SLEEP" },
        { "Đã uống {0}. Hồi phục +{1} Thể Lực, nhưng hồi Thể Lực chậm lại trong 120 giây!", "Drank {0}. Restored +{1} Stamina, but stamina regen is slower for 120 seconds!" },
        { "Đã dùng {0}. +25 Máu!", "Used {0}. +25 HP!" },
        { "Bản thiết kế này bị khóa. Hãy đến Thư Viện tìm hiểu thêm!", "This blueprint is locked. Visit the Library to learn more!" },
        { "Thủ Thư", "Librarian" },
        { "Con chưa đủ {0}🪙 để học. Hãy quay lại khi có đủ vàng nhé.", "You don't have enough {0}🪙 yet. Come back when you have enough gold." },
        { "Con đã học được bản thiết kế {0}! Giờ con có thể xây nó ở bất cứ đâu.", "You learned the {0} blueprint! Now you can build it anywhere." },
        { "Đã mở khóa: {0}", "Unlocked: {0}" },
        { "Chọn 1-9 để học, nhấn E để đóng", "Press 1-9 to learn, press E to close" },
        { "  {0}. {1} - {2}🪙", "  {0}. {1} - {2}🪙" },
        { "  • {0} - {1}🪙", "  • {0} - {1}🪙" },
        { "--- Gợi Ý ---", "--- Tips ---" },
        { "Mở khóa ngày {0}: {1}", "Unlocks on day {0}: {1}" },
        { "Bạn đã hoàn thành mọi nhiệm vụ cốt truyện. Hãy khám phá các kết thúc khác của câu chuyện!", "You've completed every story quest. Explore the other endings of the story!" },
        { "Gợi ý: Nói chuyện với Jessica mỗi ngày và hoàn thành nhiệm vụ của cô ấy để tăng Độ Thân Mật. Đạt 70+ để cầu hôn.", "Tip: Talk to Jessica every day and complete her quests to raise Affection. Reach 70+ to propose." },

        // Ending tree (settings menu)
        { "Cây Kết Thúc", "Ending Tree" },
        { "CÂY KẾT THÚC", "ENDING TREE" },
        { "Cần hoàn thành", "Must complete" },
        { "Cần im lặng", "Must stay silent" },
        { "Đã hoàn thành", "Completed" },
        { "Chưa hoàn thành", "Not completed" },
        { "Điều kiện hoàn thành:", "Completion conditions:" },
        { "Không có điều kiện", "No conditions" },
        { "Phát Kết Thúc", "Play Ending" },
        { "Chưa mở khóa", "Not unlocked" },
        { "Nhận hối lộ của Phú Ông", "Accept the rich man's bribe" },
        { "Chết khi giao chiến Quỷ Vương", "Die while fighting the Demon King" },
        { "- Diệt Quỷ Vương, chưa lật tẩy bí mật Phú Ông\n  -> QUỶ VƯƠNG ĐÃ CHẾT NHƯNG CÁI ÁC CHƯA HẾT\n\n- Lật tẩy bí mật -> báo cảnh sát (trước khi diệt Quỷ Vương)\n  -> CÔNG LÝ ĐƯỢC THỰC THI NHƯNG HIỂM HỌA CHƯA QUA\n\n- Lật tẩy bí mật -> nhận hối lộ\n  -> KẾT THÚC ĐỒI BẠI\n\n- Diệt Quỷ Vương -> báo cảnh sát\n  HOẶC Hoàn thành chuỗi Jessica (xây dinh thự)\n  -> KẾT THÚC HẠNH PHÚC\n\n- Chỉ hoàn thành chuỗi Jessica, chưa diệt Quỷ Vương,\n  chưa lật tẩy bí mật Phú Ông\n  -> KẾT THÚC ĐỊNH MỆNH\n\n- Bỏ bê Jessica 3 ngày, độ thân mật thấp\n  -> KẾT THÚC NTR\n\n- Chết khi giao chiến Quỷ Vương\n  -> RƠI VÀO BÓNG TỐI\n\n- Chết\n  -> KẾT THÚC BUỒN", "- Slay the Demon King without exposing the rich man's secret\n  -> THE DEMON IS DEAD BUT NOT ALL EVIL IS GONE\n\n- Expose secret -> report to police (before slaying the Demon King)\n  -> JUSTICE SERVED BUT NOT ALL THREAT IS GONE\n\n- Expose secret -> take the bribe\n  -> CORRUPTED ENDING\n\n- Slay the Demon King -> report to police\n  OR Complete Jessica's chain (build the mansion)\n  -> HAPPY ENDING\n\n- Complete only Jessica's chain, without slaying the Demon King\n  or exposing the rich man's secret\n  -> FATED ENDING\n\n- Neglect Jessica 3 days, low affection\n  -> NTR ENDING\n\n- Die while fighting the Demon King\n  -> FALL INTO THE DARK\n\n- Die\n  -> SAD ENDING" },

        // Building sign labels
        { "QUÁN CÀ PHÊ", "CAFE" },
        { "TIỆN LỢI", "CONVENIENCE" },
        { "THƯ VIỆN", "LIBRARY" },
        { "DANCE NIGHT", "DANCE NIGHT" },
        { "CẢNH SÁT", "POLICE" },
        { "NHÀ HÀNG", "RESTAURANT" },
        { "CỬA HÀNG", "SHOP" },
        { "CỬA HÀNG CÂU CÁ", "FISHING SHOP" },

        // Blueprint labels
        { "Cần:", "Need:" },
        { "Gỗ:", "Wood:" },
        { "Đá:", "Stone:" },

        // Interaction prompts
        { "Nói chuyện", "Talk" },
        { "Tương tác", "Interact" },
        { "Mua sắm", "Shop" },
        { "Cầu nguyện", "Pray" },
        { "Đọc sách", "Read" },
        { "Mở cửa", "Open" },
        { "Chặt", "Chop" },
        { "Đào", "Mine" },
        { "Cày", "Till" },
        { "Đánh", "Strike" },
        { "Gọi xe buýt", "Call bus" },
        { "Gọi dân", "Call villagers" },
        { "Kích hoạt", "Activate" },
        { "Chạm để xem hàng", "Tap to view goods" },
        { "Đã dùng Mồi Bả!", "Used fish chum!" },
        { "Đã dùng Mồi Câu!", "Used fishing bait!" },
        { "Bạn có muốn ngủ để qua thời gian?", "Do you want to sleep to pass the time?" },
        { "Bạn đã ngủ qua đêm.", "You slept through the night." },
        { "Chạm màn hình để dậy", "Tap screen to wake up" },
        { "Nhấn Shift để dậy", "Press Shift to wake up" },
        { "Tôi thích lúa mì!", "I like wheat!" },
        { "Cơm Gà là đặc sản của quán ta đấy.", "Chicken Rice is the specialty here." },
        { "Cứ xem thực đơn đi, chọn món nào cũng ngon.", "Take a look at the menu — everything is delicious." },
        { "Cà phê đen nóng hổi đây, ai cần tỉnh táo cày ruộng không?", "Hot black coffee here! Anyone need to stay sharp for the fields?" },
        { "Uống xong tỉnh cả người, nhưng mà say cà phê thì hồi sức hơi chậm đấy.", "It wakes you right up, but the coffee crash slows your recovery a bit." },
        { "Cứ thử một ly xem sao!", "Give a cup a try!" },
        { "Chào mừng đến Thư Viện làng. Nơi đây cất giữ mọi bản thiết kế của vùng quê.", "Welcome to the village Library. All the blueprints of the countryside are kept here." },
        { "Muốn xây dựng điều gì mới, con cần học hỏi từ những trang sách cũ.", "To build something new, you must learn from the pages of old books." },
        { "Với chút vàng bạc, ta có thể truyền thụ tri thức về những công trình chưa từng được xây trong làng.", "For a bit of gold, I can teach you blueprints never built in this village before." },
        { "Con cứ tự do tham khảo sách. Khi đã sẵn sàng học điều mới, hãy gọi ta.", "Browse the books freely. When you're ready to learn something new, call me." },
        { "CON MUỐN HỌC BẢN THIẾT KẾ NÀO?", "WHICH BLUEPRINT DO YOU WANT TO LEARN?" },
        { "Khi nào con muốn học thêm, hãy quay lại đây nhé.", "Come back whenever you want to learn more." },
        { "Con đã nắm được mọi tri thức ở thư viện này rồi. Hãy truyền lại cho thế hệ sau nhé.", "You've mastered all the knowledge in this library. Pass it on to the next generations." },
        { "[Thiền Định] Nhấn T", "[Meditate] Press T" },
        { "[Mở Cửa Hàng] (Chạm)", "[Open Shop] (Tap)" },
        { "[Mở Cửa Hàng] Nhấn T", "[Open Shop] Press T" },
        { "Anh dạo này bận quá... em nhớ anh.", "You've been so busy lately... I miss you." },
        { "Em nghe nói ông chú giàu có kia cứ quanh quẩn gần nhà...", "I heard that rich uncle keeps hanging around the house..." },
        { "Anh không còn quan tâm em nữa sao? Ông ta đã ngỏ lời mời em đi...", "Don't you care about me anymore? He's been asking me to go with him..." },
        { "Câu 3 con cá hôm nay.", "Catch 3 fish today." },
        { "Ba đợt quái vật tấn công dồn dập!", "Three waves of monsters attack in force!" },
        { "Kẻ nghiện này... có vẻ liên quan đến gia tộc giàu có.", "This addict... seems connected to the wealthy family." },
        { "Cà Phê Đen", "Black Coffee" },
        { "Mồi Câu", "Fishing Bait" },
        { "Mồi Bả", "Fish Chum" },
        { "Quán Cà Phê", "Coffee Shop" },
        { "Hoàn thành 'Trừ Tà Quanh Chùa' và nói chuyện với thầy ở chùa", "Complete 'Cleanse The Monastery' and talk to the monk at the pagoda" },
        { "Tỏ tình với Jessica và cô ấy đồng ý", "Confess to Jessica and she accepts" },
        { "Tự động mở khóa", "Unlocked automatically" },
        { "Điều kiện mở khóa:", "Unlock conditions:" },
        { "Kết thúc liên quan:", "Related ending:" },
        { "Lựa chọn đặc biệt:", "Special choice:" }
    };
}
