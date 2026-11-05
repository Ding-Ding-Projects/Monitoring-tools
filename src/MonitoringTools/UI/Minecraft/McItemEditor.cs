using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    /// <summary>
    /// The item component editor of the command builder: a custom name, lore, enchantments with levels, damage, unbreakable
    /// and custom model data as controls, and a generic row for any other component, whose id comes from the version's own
    /// data component registry and whose value is checked as SNBT. It writes [component=value,...] in the form the version takes.
    /// </summary>
    internal sealed class McItemEditor : StackPanel
    {
        private readonly string _version;
        private readonly McRegistries _reg;
        private readonly Action<string> _changed;
        private readonly List<McComponent> _parts;
        private readonly TextBlock _problem, _preview;
        private readonly StackPanel _enchants = new StackPanel(), _other = new StackPanel();

        private static readonly string[] Special = { "minecraft:custom_name", "minecraft:lore", "minecraft:enchantments", "minecraft:damage", "minecraft:unbreakable", "minecraft:custom_model_data" };

        public string Components => McItemComponents.Compose(_parts);

        public McItemEditor(string version, McRegistries reg, string rest, Action<string> changed)
        {
            _version = version; _reg = reg; _changed = changed;
            AutomationProperties.SetName(this, "Item components");
            _parts = McItemComponents.ReadList(rest, out _) ?? new List<McComponent>();
            for (int i = 0; i < _parts.Count; i++) _parts[i].Key = McItemComponents.Namespaced(_parts[i].Key.TrimStart('!')).Insert(0, _parts[i].Key.StartsWith("!") ? "!" : "");
            // A component the controls cannot read back stays in the generic list, untouched.
            Func<string, string> val = k => _parts.FirstOrDefault(p => p.Key == k)?.Value;

            var nameBox = McCommandControls.Text(val("minecraft:custom_name") is string cn ? McItemComponents.UnText(version, cn) ?? "" : "", "Custom name", 320, "A name shown instead of the item's own");
            nameBox.TextChanged += (o, e) => Set("minecraft:custom_name", nameBox.Text.Length == 0 ? null : McItemComponents.Text(version, nameBox.Text));
            Add("Custom name", nameBox, "Replaces the name shown for the item.");

            var lore = McCommandControls.Text(val("minecraft:lore") is string lv ? string.Join("\n", McItemComponents.UnLore(version, lv) ?? new List<string>()) : "", "Lore, one line per row", 320, "Lines of text under the item's name");
            lore.AcceptsReturn = true; lore.TextWrapping = TextWrapping.Wrap; lore.MinHeight = 64; lore.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            lore.TextChanged += (o, e) =>
            {
                var lines = lore.Text.Replace("\r", "").Split('\n').Where(l => l.Length > 0).ToList();
                Set("minecraft:lore", lines.Count == 0 ? null : McItemComponents.Lore(version, lines));
            };
            Add("Lore", lore, "Grey lines of text under the item's name. One line per row.");

            // enchantments
            string pickedEnchant = "";
            var level = new McNumberBox("Enchantment level", 1, 1, 255) { Margin = new Thickness(0, 0, 10, 8) };
            var picker = _reg?.Get("minecraft:enchantment") is { Count: > 0 }
                ? McCommandControls.Pick(new McCommandNode { Name = "enchantment", Type = "argument", Parser = "minecraft:resource_or_tag", Registry = "minecraft:enchantment" }, "minecraft:enchantment", "Enchantment", "", v => pickedEnchant = v, _reg).Item1
                : null;
            TextBox typed = null;
            if (picker == null) { typed = McCommandControls.Text("", "Enchantment id", 260, "An enchantment id such as minecraft:sharpness"); typed.TextChanged += (o, e) => pickedEnchant = typed.Text.Trim(); }
            var addEnchant = Ui.Button("Add enchantment", () =>
            {
                if (pickedEnchant.Length == 0) { Say("Choose an enchantment first."); return; }
                if (!level.Valid) { Say("The level must be from 1 to 255."); return; }
                var levels = Levels();
                levels.RemoveAll(kv => McItemComponents.Namespaced(kv.Key) == McItemComponents.Namespaced(pickedEnchant));
                levels.Add(new KeyValuePair<string, int>(McItemComponents.Namespaced(pickedEnchant), (int)level.Value));
                Set("minecraft:enchantments", McItemComponents.Enchantments(version, levels));
                PaintEnchants();
            }, "Btn.Ghost", Icons.Plus);
            var enchPanel = new StackPanel();
            enchPanel.Children.Add(_enchants);
            if (picker != null) enchPanel.Children.Add(picker); else enchPanel.Children.Add(typed);
            var lrow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            lrow.Children.Add(level); lrow.Children.Add(addEnchant);
            enchPanel.Children.Add(lrow);
            Add("Enchantments", enchPanel, "Each enchantment with its level. Adding one that is already there changes its level.");

            // damage, unbreakable, custom model data
            var damage = new McNumberBox("Damage", 0, 0, int.MaxValue) { AllowEmpty = true };
            if (val("minecraft:damage") is string dv && int.TryParse(dv, out var dn)) damage.Box.Text = dn.ToString(CultureInfo.InvariantCulture); else damage.Box.Text = "";
            damage.Box.TextChanged += (o, e) => Set("minecraft:damage", damage.Box.Text.Trim().Length == 0 || !damage.Valid ? null : damage.Box.Text.Trim());
            Add("Damage", damage, "How worn the item is, in points. Leave empty for undamaged.");

            var unbreak = McUi.Chip("Unbreakable", val("minecraft:unbreakable") != null, null, "Unbreakable");
            unbreak.Click += (o, e) => { bool on = val("minecraft:unbreakable") == null; McUi.Paint(unbreak, on); Set("minecraft:unbreakable", on ? "{}" : null); };
            Add("Unbreakable", unbreak, "The item never loses durability.");

            var model = new McNumberBox("Custom model data", 0, 0, int.MaxValue) { AllowEmpty = true };
            model.Box.Text = val("minecraft:custom_model_data") is string mv && McItemComponents.UnModelData(version, mv) is int mn ? mn.ToString(CultureInfo.InvariantCulture) : "";
            model.Box.TextChanged += (o, e) =>
            {
                bool empty = model.Box.Text.Trim().Length == 0 || !model.Valid;
                Set("minecraft:custom_model_data", empty ? null : McItemComponents.ModelData(version, (int)model.Value));
            };
            Add("Custom model data", model, McItemComponents.CompoundModelData(version) ? "A number a resource pack can use to pick a different model. Written as the first float, which this version reads." : "A number a resource pack can use to pick a different model.");

            // any other component
            var key = _reg?.Get(McItemComponents.Registry) is { Count: > 0 }
                ? McCommandControls.Pick(new McCommandNode { Name = "component", Type = "argument", Parser = "minecraft:resource_or_tag", Registry = McItemComponents.Registry }, McItemComponents.Registry, "Component", "", v => _genericKey = v, _reg).Item1
                : null;
            var keyBox = McCommandControls.Text("", "Component id", 280, "A component id such as minecraft:max_stack_size");
            keyBox.TextChanged += (o, e) => _genericKey = keyBox.Text.Trim();
            var valueBox = McCommandControls.Text("", "Component value", 280, "The value as SNBT, like 16 or {} or \"text\"");
            var addOther = Ui.Button("Add component", () =>
            {
                if (_genericKey.Length == 0) { Say("Choose a component first."); return; }
                if (Special.Contains(McItemComponents.Namespaced(_genericKey))) { Say(McItemComponents.Namespaced(_genericKey) + " has its own control above."); return; }
                var v = valueBox.Text.Trim();
                if (v.Length == 0 || !McSnbt.LooksValid(v)) { Say("The value is not valid SNBT. Numbers, \"text\", {…} and […] are."); return; }
                var full = McItemComponents.Namespaced(_genericKey);
                _parts.RemoveAll(p => p.Key == full);
                _parts.Add(new McComponent { Key = full, Value = v });
                valueBox.Text = "";
                Changed(); PaintOther();
            }, "Btn.Ghost", Icons.Plus);
            var otherPanel = new StackPanel();
            otherPanel.Children.Add(_other);
            if (key != null) otherPanel.Children.Add(key); else otherPanel.Children.Add(keyBox);
            var orow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            orow.Children.Add(valueBox); orow.Children.Add(addOther);
            otherPanel.Children.Add(orow);
            Add("Other components", otherPanel, _reg?.Get(McItemComponents.Registry) is { Count: > 0 } l ? $"Any of the {l.Count} components this version lists. The value is SNBT." : "This version's component list has not been read, so type the id. The value is SNBT.");

            _problem = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.Danger"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 0, 0), Visibility = Visibility.Collapsed };
            AutomationProperties.SetLiveSetting(_problem, AutomationLiveSetting.Polite);
            _preview = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 0, 0) };
            Core.Language.SetLiteral(_preview, true);
            Children.Add(_problem); Children.Add(_preview);
            PaintEnchants(); PaintOther(); Show();
        }

        private string _genericKey = "";

        private void Add(string title, FrameworkElement control, string help)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(control);
            sp.Children.Add(new TextBlock { Text = help, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 2, 0, 0) });
            Children.Add(sp);
        }

        private void Say(string text) { _problem.Text = text; _problem.Visibility = Visibility.Visible; Core.Language.Refresh(_problem); }

        private void Set(string key, string value)
        {
            _parts.RemoveAll(p => p.Key == key);
            if (value != null) _parts.Add(new McComponent { Key = key, Value = value });
            Changed();
        }

        private void Changed()
        {
            Show();
            _changed(Components);
        }

        private void Show()
        {
            var bad = McItemComponents.Check(_version, Components, _reg);
            _problem.Text = bad ?? ""; _problem.Visibility = bad == null ? Visibility.Collapsed : Visibility.Visible;
            _preview.Text = Components.Length == 0 ? "No components. The item is written as just its id." : Components;
            Core.Language.Refresh(_problem); Core.Language.Refresh(_preview);
        }

        private List<KeyValuePair<string, int>> Levels() =>
            (_parts.FirstOrDefault(p => p.Key == "minecraft:enchantments") is McComponent c ? McItemComponents.UnEnchantments(_version, c.Value) : null) ?? new List<KeyValuePair<string, int>>();

        private void PaintEnchants()
        {
            _enchants.Children.Clear();
            var wrap = new WrapPanel();
            AutomationProperties.SetName(wrap, "Enchantments on the item");
            foreach (var kv in Levels())
            {
                var id = kv.Key;
                var chip = McUi.Chip($"{McRegistries.Readable(id)} {kv.Value}  ✕", true, () =>
                {
                    var rest = Levels(); rest.RemoveAll(x => x.Key == id);
                    Set("minecraft:enchantments", rest.Count == 0 ? null : McItemComponents.Enchantments(_version, rest));
                    PaintEnchants();
                }, $"Remove {McRegistries.Readable(id)} {kv.Value}");
                wrap.Children.Add(chip);
            }
            if (wrap.Children.Count > 0) _enchants.Children.Add(wrap);
            Core.Language.Refresh(_enchants);
        }

        private void PaintOther()
        {
            _other.Children.Clear();
            var wrap = new WrapPanel();
            AutomationProperties.SetName(wrap, "Other components on the item");
            foreach (var p in _parts.Where(p => !Special.Contains(p.Key) || !Decodes(p)).ToList())
            {
                var part = p;
                var chip = McUi.Chip($"{part.Key.Replace("minecraft:", "")}={(part.Value.Length > 24 ? part.Value[..24] + "…" : part.Value)}  ✕", true, () => { _parts.Remove(part); Changed(); PaintOther(); }, "Remove component " + part.Key);
                Core.Language.SetLiteral(chip, true);
                wrap.Children.Add(chip);
            }
            if (wrap.Children.Count > 0) _other.Children.Add(wrap);
            Core.Language.Refresh(_other);
        }

        // true when its own control shows the component, so it does not appear twice
        private bool Decodes(McComponent p) => p.Key switch
        {
            "minecraft:custom_name" => McItemComponents.UnText(_version, p.Value) != null,
            "minecraft:lore" => McItemComponents.UnLore(_version, p.Value) != null,
            "minecraft:enchantments" => McItemComponents.UnEnchantments(_version, p.Value) != null,
            "minecraft:damage" => int.TryParse(p.Value, out _),
            "minecraft:unbreakable" => true,
            "minecraft:custom_model_data" => McItemComponents.UnModelData(_version, p.Value) != null,
            _ => false,
        };
    }
}
