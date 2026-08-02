using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;

using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace NightmareUI.ImGuiElements;
public class WorldSelector
{
    private static WorldSelector? _instance;
    public static WorldSelector Instance
    {
        get
        {
            _instance ??= new();
            return _instance;
        }
    }

    private string WorldFilter = "";
    private bool WorldFilterActive = false;

    public string EmptyName { get; set; } = null;
    public bool DisplayCurrent { get; set; } = false;
    public bool DefaultAllOpen { get; set; } = false;
    public Predicate<uint>? ShouldHideWorld { get; set; } = null;

    private string ID;

    public WorldSelector(string id = "##world")
    {
        ID = id;
    }

    // 2026-08 補記:四個消費端(AutoRetainer/TextAdvance/Lifestream/Splatoon)現在都已經把
    // ECommons pin 到 db0ceca7 以後——那個版本的 ExcelWorldHelper 已經在根因修好了台服世界
    // (IsPublic()/Get() 直接放行 4028-4035),並新增了 AllRegions()/GetRegionDisplayName()
    // 可以取代下面這整段本地的 TCRegionByte/TCOfficialWorldIds/GetRegionLabel 客製邏輯。
    // 目前刻意維持現狀、不做這個重構:(1) 這裡的地區標籤是簡短代碼(JP/NA/EU/OC/TW),
    // ECommons.GetRegionDisplayName() 回傳的是完整名稱(Japan/North-America/.../Taiwan),
    // 換掉會改變四個外掛的世界選單顯示文字,不是純內部重構;(2) 現在是雙重防護,兩條路徑
    // 邏輯等價、沒有壞,清理的風險(要同步改兩條分支 tw-worldselector-public-fix 與
    // -splatoon、再逐一重新 pin 並建置驗證四個消費端)不成比例於純粹的程式碼精簡收益。
    // 之後若有人已經在改這個檔案且要順手做這個整併,再一併處理即可。
    /// <summary>
    /// 台服(TC)資料中心「陸行鳥」(<c>WorldDCGroupType.RowId</c> 151)的 <c>Region</c> 欄位是 8,
    /// 不在 ECommons.ExcelServices.ExcelWorldHelper.Region 列舉(JP=1/NA=2/EU=3/OC=4)裡,
    /// 國際服的資料不會出現這個值。這裡把它當成本檔內部多出來的第五個「地區」桶,
    /// 讓下面既有的分組/展開/搜尋邏輯原封不動地把它當一般地區處理;國際服環境下
    /// 這個桶底下不會有任何世界(見 <see cref="GetWorldsForDc"/>),會被既有的
    /// <c>Sum() &gt; 0</c> 判斷整段跳過,零副作用。
    /// </summary>
    private const byte TCRegionByte = 8;
    private static readonly ExcelWorldHelper.Region TCRegion = (ExcelWorldHelper.Region)TCRegionByte;

    private static string GetRegionLabel(ExcelWorldHelper.Region region)
        => region == TCRegion ? "TW" : region.ToString();

    /// <summary>
    /// 台服 8 個正式世界(伊弗利特/迦樓羅/利維坦/鳳凰/奧汀/巴哈姆特/拉姆/泰坦,
    /// RowId 4028–4035,DC 151)在 <c>World.IsPublic</c> 全部是 False(2026-08 台服 7.20
    /// EXD 實測),直接呼叫 <see cref="ExcelWorldHelper.GetPublicWorlds(uint)"/> 會讓
    /// 世界選單在台服一個世界都列不出來。這裡改成直接查 <c>World</c> 表:同一個 DC
    /// 底下,公開世界(<c>IsPublic</c>)或台服正式世界(RowId 落在上述範圍)都算數。
    /// 國際服的世界 RowId 不會落在這個範圍、DC 151 也只存在於台服環境,對國際服
    /// 行為零副作用。刻意不修改 ECommons,保持本檔自包含。
    /// </summary>
    private static readonly uint[] TCOfficialWorldIds = [4028, 4029, 4030, 4031, 4032, 4033, 4034, 4035];

    private static IEnumerable<World> GetWorldsForDc(uint dcRowId)
    {
        return Svc.Data.GetExcelSheet<World>()!
            .Where(w => w.DataCenter.RowId == dcRowId && (w.IsPublic() || TCOfficialWorldIds.Contains(w.RowId)));
    }

    public void Draw(ref int worldConfig, ImGuiComboFlags flags = ImGuiComboFlags.HeightLarge)
    {
        ImGui.PushID(ID);
        string name;
        if(worldConfig == 0)
        {
            name = EmptyName ?? "Not selected";
        }
        else
        {
            name = ExcelWorldHelper.GetName((uint)worldConfig);
        }
        if(ImGui.BeginCombo("", name, flags))
        {
            DrawInternal(ref worldConfig);
            ImGui.EndCombo();
        }
        ImGui.PopID();
    }

    public void DrawInternal(ref int worldConfig)
    {
        ImGuiEx.SetNextItemFullWidth();
        if(ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.InputTextWithHint($"##worldfilter", "Search...", ref WorldFilter, 50);
        Dictionary<ExcelWorldHelper.Region, Dictionary<uint, List<uint>>> regions = [];
        foreach(var region in Enum.GetValues<ExcelWorldHelper.Region>().Append(TCRegion))
        {
            regions[region] = [];
            foreach(var dc in Svc.Data.GetExcelSheet<WorldDCGroupType>()!)
            {
                if(dc.Region == (byte)region)
                {
                    regions[region][dc.RowId] = [];
                    foreach(var world in GetWorldsForDc(dc.RowId))
                    {
                        if(WorldFilter == "" || world.Name.ToString().Contains(WorldFilter, StringComparison.OrdinalIgnoreCase) || world.RowId.ToString().Contains(WorldFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if(ShouldHideWorld == null || !ShouldHideWorld(world.RowId))
                            {
                                regions[region][dc.RowId].Add(world.RowId);
                            }
                        }
                    }
                    regions[region][dc.RowId] = [.. regions[region][dc.RowId].OrderBy(ExcelWorldHelper.GetName)];
                }
            }
        }
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 1));
        if(EmptyName != null)
        {
            ImGui.SetNextItemOpen(false);
            if(ImGuiEx.TreeNode($"{EmptyName}##empty", ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet | (0 == worldConfig ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None)))
            {
                worldConfig = 0;
                ImGui.CloseCurrentPopup();
            }
        }
        if(DisplayCurrent && Player.Available)
        {
            ImGui.SetNextItemOpen(false);
            if(ImGuiEx.TreeNode(ImGuiColors.DalamudViolet, $"Current: {ExcelWorldHelper.GetName(Player.Object.CurrentWorld.RowId)}", ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet | (Player.Object.CurrentWorld.RowId == worldConfig ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None)))
            {
                worldConfig = (int)Player.Object.CurrentWorld.RowId;
                ImGui.CloseCurrentPopup();
            }
        }
        foreach(var region in regions)
        {
            if(region.Value.Sum(dc => dc.Value.Count) > 0)
            {
                if(WorldFilter != "" && (!WorldFilterActive || ImGui.IsWindowAppearing()))
                {
                    ImGui.SetNextItemOpen(true);
                }
                else if(ImGui.IsWindowAppearing() || (WorldFilter == "" && WorldFilterActive))
                {
                    var w = worldConfig;
                    if(region.Value.Any(d => d.Value.Contains((uint)w)))
                    {
                        ImGui.SetNextItemOpen(true);
                    }
                    else
                    {
                        ImGui.SetNextItemOpen(false);
                        foreach(var v in region.Value)
                        {
                            ImGui.PushID($"{region.Key}");
                            ImGui.GetStateStorage().SetInt(ImGui.GetID($"{Svc.Data.GetExcelSheet<WorldDCGroupType>()!.GetRowOrDefault(v.Key)?.Name}"), 0);
                            ImGui.PopID();
                        }
                    }
                }
                if(DefaultAllOpen && ImGui.IsWindowAppearing()) ImGui.SetNextItemOpen(true);
                if(ImGuiEx.TreeNode(GetRegionLabel(region.Key)))
                {
                    foreach(var dc in region.Value)
                    {
                        if(dc.Value.Count > 0)
                        {
                            if(WorldFilter != "" && (!WorldFilterActive || ImGui.IsWindowAppearing()))
                            {
                                ImGui.SetNextItemOpen(true);
                            }
                            else if(ImGui.IsWindowAppearing() || (WorldFilter == "" && WorldFilterActive))
                            {
                                if(dc.Value.Contains((uint)worldConfig))
                                {
                                    ImGui.SetNextItemOpen(true);
                                }
                                else
                                {
                                    ImGui.SetNextItemOpen(false);
                                }
                            }
                            if(DefaultAllOpen && ImGui.IsWindowAppearing()) ImGui.SetNextItemOpen(true);
                            if(ImGuiEx.TreeNode($"{Svc.Data.GetExcelSheet<WorldDCGroupType>()!.GetRowOrDefault(dc.Key)?.Name}"))
                            {
                                foreach(var world in dc.Value)
                                {
                                    ImGui.SetNextItemOpen(false);
                                    if(ImGuiEx.TreeNode($"{ExcelWorldHelper.GetName(world)}", ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet | (world == worldConfig ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None)))
                                    {
                                        worldConfig = (int)world;
                                        ImGui.CloseCurrentPopup();
                                    }
                                }
                                ImGui.TreePop();
                            }
                        }
                    }
                    ImGui.TreePop();
                }
            }
        }
        ImGui.PopStyleVar();
        WorldFilterActive = WorldFilter != "";
    }
}
