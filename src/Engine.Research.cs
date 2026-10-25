using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        public void InitializeTechnology(Campaign campaign, bool legacy)
        {
            foreach (var n in campaign.Nations) n.ScienceTree = new ResearchAgenda {
                LegacyLevels = legacy ? (int[])n.Tech.Clone() : new int[6], ActiveProject = n.Research
            };
            campaign.Version = 7;
        }
        public string ResearchReason(int project, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (owner >= S.Nations.Length) return "Неизвестное государство.";
            var n = S.Nations[owner]; return TechnologyRules.Requirements(project, n.Tech, n.ScienceTree.Known);
        }
        public string StartResearch(int branch)
        {
            if (branch < 0 || branch >= 6) return "Неизвестная отрасль.";
            if (Player.Research >= 0) return "Сначала завершите или смените текущую программу.";
            return SelectResearch(branch);
        }
        public string SelectResearch(int project, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            string reason = ResearchReason(project, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; var agenda = n.ScienceTree;
            if (agenda.ActiveProject == project) return "Эта программа уже выбрана.";
            if (agenda.ActiveProject >= 0) agenda.StoredProgress[agenda.ActiveProject] = n.ResearchProgress;
            BeginResearch(n, project); agenda.Queue.Remove(project); agenda.Paused = false;
            if (owner == S.Player) Notify("Исследование: " + TechnologyRules.Name(project) + ". Прогресс прежней программы сохранён.");
            Raise(); return null;
        }
        private void BeginResearch(Nation n, int project)
        {
            n.ScienceTree.ActiveProject = project; n.Research = TechnologyRules.Branch(project);
            n.ResearchProgress = n.ScienceTree.StoredProgress[project]; n.ScienceTree.StoredProgress[project] = 0;
        }
        public string QueueResearchReason(int project, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (!TechnologyRules.ValidProject(project) || owner >= S.Nations.Length) return "Неизвестная программа или государство.";
            var n = S.Nations[owner]; var a = n.ScienceTree;
            if (a.Queue.Count >= 4) return "В плане не более четырёх следующих программ.";
            if (a.Queue.Contains(project) || (project >= 6 && a.ActiveProject == project)) return "Эта технология уже в работе или в плане.";
            var levels = (int[])n.Tech.Clone(); var known = (bool[])a.Known.Clone();
            Action<int> finish = id => { if (id < 6) levels[id]++; else known[id - 6] = true; };
            if (a.ActiveProject >= 0) finish(a.ActiveProject);
            foreach (int planned in a.Queue) {
                if (TechnologyRules.Requirements(planned, levels, known) != null) return "Сначала исправьте порядок предпосылок в существующем плане.";
                finish(planned);
            }
            return TechnologyRules.Requirements(project, levels, known);
        }
        public string QueueResearch(int project, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            string reason = QueueResearchReason(project, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; n.ScienceTree.Queue.Add(project); TryBeginQueuedResearch(n);
            Raise(); return null;
        }
        public string RemoveResearchQueue(int index)
        {
            var a = Player.ScienceTree;
            if (index < 0 || index >= a.Queue.Count) return "Пункт плана не найден.";
            a.Queue.RemoveAt(index); Raise(); return null;
        }
        public void PauseResearch(bool paused)
        {
            Player.ScienceTree.Paused = paused; if (!paused) TryBeginQueuedResearch(Player); Raise();
        }
        private void TryBeginQueuedResearch(Nation n)
        {
            var a = n.ScienceTree;
            if (a.Paused || a.ActiveProject >= 0 || a.Queue.Count == 0) return;
            int project = a.Queue[0];
            if (TechnologyRules.Requirements(project, n.Tech, a.Known) != null) return;
            a.Queue.RemoveAt(0); BeginResearch(n, project);
        }
        public double WeeklyResearchRate(int owner, int project)
        {
            if (!TechnologyRules.ValidProject(project)) return 0;
            var n = S.Nations[owner]; if (n.Science <= 0) return 0;
            double work = project < 6 ? 1 : TechnologyRules.Catalog[project - 6].Work;
            return (1 + n.Science * .7) * (n.Education / 60) / (1 + n.Tech[TechnologyRules.Branch(project)] * .18) / work
                * PolicyRules.Effects(n, S.Day).ResearchFactor * (1 + TechnologyRules.Effects(n).Research);
        }
        public string ResearchStatus(int owner)
        {
            var n = S.Nations[owner]; var a = n.ScienceTree;
            if (a.Paused) return "Исследования приостановлены";
            if (a.ActiveProject < 0) return a.Queue.Count == 0 ? "Выберите программу" : ResearchReason(a.Queue[0], owner);
            if (n.Science <= 0) return "Нет научного финансирования";
            return TechnologyRules.CurrentName(n) + " · " + n.ResearchProgress.ToString("0") + "%";
        }
        private void Research()
        {
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                var n = S.Nations[owner]; var a = n.ScienceTree; TryBeginQueuedResearch(n);
                if (a.Paused || a.ActiveProject < 0) continue;
                int project = a.ActiveProject; n.ResearchProgress += WeeklyResearchRate(owner, project);
                if (n.ResearchProgress < 100) continue;
                if (project < 6) n.Tech[project]++; else a.Known[project - 6] = true;
                a.StoredProgress[project] = 0; a.ActiveProject = -1; n.Research = -1; n.ResearchProgress = 0;
                if (owner == S.Player) Notify("Внедрено: " + TechnologyRules.Name(project) + (project < 6 ? " · уровень " + n.Tech[project] : ""), 1);
                TryBeginQueuedResearch(n);
            }
        }
        internal void PlanResearch(int owner)
        {
            var n = S.Nations[owner]; if (n.ScienceTree.ActiveProject >= 0 || n.ScienceTree.Paused) return;
            TryBeginQueuedResearch(n); if (n.ScienceTree.ActiveProject >= 0 || n.ScienceTree.Queue.Count > 0) return;
            int phase = n.Focus++; int branch = n.Shortage > .1 ? 0 : NationAtWar(owner) && phase % 2 == 0 ? 5 : phase % 6;
            int desired = phase % 3 == 0 ? branch : TechnologyRules.Catalog.Where(t => t.Branch == branch && !n.ScienceTree.Known[t.Id]).Select(t => t.Id + 6).DefaultIfEmpty(branch).First();
            int next = NextResearchPrerequisite(n, desired);
            if (ResearchReason(next, owner) == null) SelectResearch(next, owner);
        }
        private int NextResearchPrerequisite(Nation n, int project)
        {
            if (project < 6 || TechnologyRules.Requirements(project, n.Tech, n.ScienceTree.Known) == null) return project;
            var d = TechnologyRules.Catalog[project - 6];
            if (n.Tech[d.Branch] < d.Foundation) return d.Branch;
            foreach (int id in d.Prerequisites) if (!n.ScienceTree.Known[id]) return NextResearchPrerequisite(n, id + 6);
            return d.Branch;
        }
        private void ValidateTechnology(Nation n)
        {
            var a = n.ScienceTree;
            if (a == null || a.Known == null || a.Known.Length != 24 || !Values(a.StoredProgress, 30) || a.StoredProgress.Any(p => p >= 100)
                || a.LegacyLevels == null || a.LegacyLevels.Length != 6 || a.LegacyLevels.Where((v, i) => v < 0 || v > n.Tech[i]).Any()
                || a.ActiveProject < -1 || a.ActiveProject >= 30 || (a.ActiveProject < 0 ? n.Research != -1 || n.ResearchProgress != 0 : n.Research != TechnologyRules.Branch(a.ActiveProject) || a.StoredProgress[a.ActiveProject] != 0)
                || n.ResearchProgress < 0 || n.ResearchProgress >= 100 || a.Queue == null || a.Queue.Count > 4 || a.Queue.Any(p => !TechnologyRules.ValidProject(p)) || a.Queue.Distinct().Count() != a.Queue.Count)
                throw new InvalidDataException("Повреждён план исследований.");
            foreach (var d in TechnologyRules.Catalog) {
                bool started = a.Known[d.Id] || a.StoredProgress[d.Id + 6] > 0;
                if (started && (n.Tech[d.Branch] < d.Foundation || d.Prerequisites.Any(id => !a.Known[id])) || a.Known[d.Id] && a.StoredProgress[d.Id + 6] != 0)
                    throw new InvalidDataException("Нарушены предпосылки технологий.");
            }
            if (a.ActiveProject >= 0 && TechnologyRules.Requirements(a.ActiveProject, n.Tech, a.Known) != null) throw new InvalidDataException("Невозможное текущее исследование.");
            if (a.Queue.Any(p => p >= 6 && (a.Known[p - 6] || p == a.ActiveProject))) throw new InvalidDataException("Повторное исследование освоенной технологии.");
        }
    }
}
