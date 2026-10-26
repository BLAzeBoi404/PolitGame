using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Sovereign
{
    public partial class GameForm
    {
        private bool procurementPage;
        private int procurementCountry = -1, procurementCompany = -1, procurementProduct = 8, procurementQuantity = 5, procurementFactories = 1, procurementOffset, supplierOffset;
        private void OpenProcurement() { Navigate("Производство"); procurementPage = true; detailPanel = true; if (procurementCountry < 0) procurementCountry = E.S.Player; Invalidate(); }
        private void ChooseSupplierCountry()
        {
            int previous = speed; speed = 0;
            try {
                using (var dialog = new Form { Text = "Страна поставщика", ClientSize = new Size(460, 145), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false }) {
                    var choices = Enumerable.Range(0, W.Countries.Length).OrderBy(i => W.Countries[i].Name).ToArray();
                    var combo = new ComboBox { Left = 18, Top = 20, Width = 424, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
                    combo.Items.AddRange(choices.Select(i => W.Countries[i].Name).ToArray()); combo.SelectedIndex = Array.IndexOf(choices, procurementCountry);
                    var ok = new System.Windows.Forms.Button { Text = "Выбрать", Left = 218, Top = 85, Width = 108, DialogResult = DialogResult.OK };
                    var cancel = new System.Windows.Forms.Button { Text = "Отмена", Left = 334, Top = 85, Width = 108, DialogResult = DialogResult.Cancel };
                    dialog.Controls.AddRange(new Control[] {combo, ok, cancel}); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) == DialogResult.OK && combo.SelectedIndex >= 0) { procurementCountry = choices[combo.SelectedIndex]; procurementCompany = -1; supplierOffset = 0; }
                }
            } finally { speed = previous; }
            Invalidate();
        }
        private void DrawProcurement(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, left = 330, right = x + left + 28, rw = w - left - 28;
            PageTitle(g, "Государственные закупки", "Частные предприятия: материалы, гражданский транспорт и техника по государственному проекту.");
            if (procurementCountry < 0) procurementCountry = E.S.Player;
            Section(g, "ПОСТАВЩИК", x, 227, left);
            Button(g, W.Countries[procurementCountry].Name + " ▾", x, 267, left, ChooseSupplierCountry, false, "Выбрать страну из списка с поиском по названию");
            Button(g, "Своё производство", x, 313, left, () => { procurementCountry = E.S.Player; supplierOffset = 0; procurementCompany = -1; Invalidate(); }, false, "Частные предприятия вашей страны", 26);
            var companies = Enumerable.Range(0, W.Regions.Length).Where(r => E.S.Provinces[r].Owner == procurementCountry).Select(r => r * 8 + ProcurementRules.Sector(procurementProduct)).Where(c => E.CompanyCapacity(c) > 0).ToList();
            if (!companies.Contains(procurementCompany)) procurementCompany = companies.FirstOrDefault();
            if (companies.Count == 0) procurementCompany = -1;
            supplierOffset = Math.Min(supplierOffset, Math.Max(0, companies.Count - 5));
            for (int i = supplierOffset; i < Math.Min(companies.Count, supplierOffset + 5); i++) {
                int company = companies[i]; float y = 365 + (i - supplierOffset) * 62;
                Button(g, W.Regions[company / 8].Name, x, y, left, () => { procurementCompany = company; Invalidate(); }, procurementCompany == company, E.CompanyName(company), 29); hits[hits.Count - 1].Label = "supplier:" + company;
                Txt(g, "Цехи занято / всего: " + E.CompanyAssigned(company) + "/" + E.CompanyCapacity(company), x + 9, y + 36, 11, Palette.Muted);
            }
            if (companies.Count == 0) Para(g, "Подходящих частных предприятий нет. Выберите другую страну или продукцию.", x, 365, left, 90, 15);
            Button(g, "←", x, 682, 40, () => { supplierOffset = Math.Max(0, supplierOffset - 5); Invalidate(); }, false, "Предыдущие предприятия", 27);
            Button(g, "→", x + 48, 682, 40, () => { supplierOffset = Math.Min(Math.Max(0, companies.Count - 5), supplierOffset + 5); Invalidate(); }, false, "Следующие предприятия", 27);
            Txt(g, companies.Count + " предприятий", x + 103, 688, 12, Palette.Muted);
            Para(g, "Цех = 0,1 завода. Предприятия — условные операторы частных мощностей областей, а не справочник реальных брендов. Материалы берутся со склада страны поставщика; оплата поступает её частному сектору.", x, 731, left, 110, 12);
            Section(g, "ПРЕДМЕТ КОНТРАКТА", right, 227, rw);
            Button(g, ProcurementRules.Products[procurementProduct] + "  ›", right, 267, rw, () => { procurementProduct = (procurementProduct + 1) % ProcurementRules.Products.Length; supplierOffset = 0; Invalidate(); }, true, "Переключить товар: 8 ресурсов, автобусы, грузовики, техника по проекту"); hits[hits.Count - 1].Label = "contract:product";
            var design = E.FindDesign(E.S.Player, E.Player.ActiveDesign);
            if (procurementProduct == 10) {
                var available = E.Player.Designs.Where(d => !d.Retired).ToList();
                AvailableButton(g, available.Count == 0 ? "Нет действующих проектов" : "Проект: " + design.Blueprint.Name + "  ›", right, 312, rw,
                    () => Message(E.SelectDesign(available[(available.FindIndex(d => d.Id == design.Id) + 1) % available.Count].Id)), available.Count > 0,
                    available.Count == 0 ? "Восстановите проект в каталоге или испытайте новый." : "Выбрать модель из вашего каталога", false, 27);
                hits[hits.Count - 1].Label = "contract:design";
            }
            else Txt(g, procurementProduct >= 8 ? "Партия = 10 машин; доставка в выбранную область" : "Партия = 1 единица государственного запаса", right, 318, 12, Palette.Muted, false, rw);
            Adjust(g, "Партий", procurementQuantity, right, 357, rw * .49f, d => procurementQuantity = Math.Max(1, Math.Min(1000, procurementQuantity + d)));
            Adjust(g, "Цехи", procurementFactories, right + rw * .52f, 357, rw * .48f, d => procurementFactories = Math.Max(1, Math.Min(500, procurementFactories + d)));
            int destination = OwnRegion(); double quote = ProcurementRules.Price(procurementProduct, design.Blueprint) * procurementQuantity;
            Txt(g, "Доставка: " + W.Regions[destination].Name, right, 407, 12, Palette.Text, false, rw);
            Txt(g, "Автобусы " + E.S.Provinces[destination].Buses + " · грузовики " + E.S.Provinces[destination].Trucks + " · обслуживание " + E.S.Provinces[destination].FleetService.ToString("P0"), right, 429, 11, Palette.Muted, false, rw);
            string reason = E.ContractReason(procurementCompany, procurementProduct, procurementQuantity, procurementFactories, destination, design.Id);
            AvailableButton(g, "Заключить контракт · " + Money(quote), right, 463, rw, () => { if (Confirm("Заказать «" + ProcurementRules.Products[procurementProduct] + "»: " + procurementQuantity + " партий, " + Money(quote) + "? Частные мощности резервируются. Оплата и материалы списываются по мере работы; отмена не возвращает понесённые затраты.")) Message(E.PlaceContract(procurementCompany, procurementProduct, procurementQuantity, procurementFactories, destination, design.Id)); }, reason == null, reason ?? "Оплата частями; недостающие материалы, война или эмбарго останавливают исполнение.", true); hits[hits.Count - 1].Label = "contract:place";
            var contracts = E.Player.Contracts; procurementOffset = Math.Min(procurementOffset, Math.Max(0, contracts.Count - 3));
            Section(g, "КОНТРАКТЫ / " + contracts.Count, right, 523, rw);
            for (int i = procurementOffset; i < Math.Min(contracts.Count, procurementOffset + 3); i++) {
                var c = contracts[i]; float y = 564 + (i - procurementOffset) * 73;
                Txt(g, "№" + c.Id + " · " + ProcurementRules.Products[c.Product], right, y, 13, Palette.Text, true, rw - 148);
                Txt(g, c.Delivered + "/" + c.Quantity + " · " + E.ContractStatus(E.S.Player, c) + " · оплачено " + Money(c.Paid), right, y + 24, 10, Palette.Muted, false, rw - 148);
                Progress(g, right, y + 48, rw - 150, (c.Delivered + c.Progress) / c.Quantity, Palette.Teal);
                AvailableButton(g, c.Paused ? "Пуск" : "Пауза", right + rw - 135, y, 65, () => Message(E.ToggleContract(c.Id)), c.Delivered < c.Quantity, "Пауза освобождает мощности", false, 26); hits[hits.Count - 1].Label = "contract:pause:" + c.Id;
                Button(g, "Убрать", right + rw - 65, y, 65, () => { if (c.Delivered == c.Quantity || Confirm("Отменить контракт №" + c.Id + "? Поставки сохранятся; оплаченная работа не возвращается.")) Message(E.CancelContract(c.Id)); }, false, "Завершённые поставки сохраняются", 26); hits[hits.Count - 1].Label = "contract:cancel:" + c.Id;
            }
            Button(g, "‹", right, 803, 35, () => { procurementOffset = Math.Max(0, procurementOffset - 3); Invalidate(); }, false, "Предыдущие контракты", 25);
            Button(g, "›", right + 42, 803, 35, () => { procurementOffset = Math.Min(Math.Max(0, contracts.Count - 3), procurementOffset + 3); Invalidate(); }, false, "Следующие контракты", 25);
            Txt(g, "Автопарк улучшает обслуживание дорог; нужны топливо и бюджет.", right + 89, 809, 10, Palette.Muted, false, rw - 89);
        }
    }
}
