# GRAND STRATEGY GAME — MAJOR GAMEPLAY, REALISM, AI AND UI OVERHAUL

## Your Role and Main Objective

You are a senior game developer, gameplay systems architect, AI engineer, UI/UX designer, and game optimization specialist working on an existing grand strategy game.

My goal is to transform this project into a deep, realistic, polished, and enjoyable grand strategy game inspired by **Hearts of Iron IV** and **Age of History 3**, while maintaining its own identity.

I want the game to feel like it was developed by a professional game studio, not like a collection of loosely connected experimental features.

The main priorities are:

- Realistic military capabilities and country differences.
- Deep, intelligent AI behavior.
- Meaningful technology and military development.
- A functioning economy, industry, and international trade.
- Detailed diplomacy and international relations.
- Improved military equipment design and customization.
- Mobilization, logistics, and military organization.
- Modern, professional, intuitive UI/UX.
- High performance, maintainable code, and long-term scalability.

**IMPORTANT:** Work with the existing project. Inspect and understand the codebase before modifying anything. Preserve functioning systems, improve existing architecture where necessary, and avoid unnecessary rewrites.

Do not simply describe how these features could work. Your task is to progressively implement real, functional improvements.

---

## 1. REALISTIC MILITARY SYSTEM AND COUNTRY CAPABILITIES

The military system needs a substantial realism overhaul.

Every country should have believable military capabilities based on its real-world circumstances, including:

- Population and available military manpower.
- Active military personnel and reserve forces.
- Realistic regiment, brigade, division, and army capacity.
- Recruitment limitations and mobilization potential.
- Military training and combat readiness.
- Equipment availability and quality.
- Technological development.
- Industrial and logistical capabilities.
- Military experience and leadership.
- Military doctrine and national strengths.
- Morale, motivation, and willingness to fight.
- Terrain knowledge, defensive preparation, and supply.

### Country differences

Countries must not feel like identical factions with different flags and starting troop numbers.

For example, Russia may possess a larger military, significant equipment reserves, and greater industrial capacity, while Ukraine may benefit from defensive motivation, increasing battlefield experience, tactical adaptation, international support, and other relevant factors.

These are examples of interacting military factors, not predetermined combat outcomes or permanent national bonuses.

A technologically advanced military should not automatically defeat every weaker opponent. Likewise, morale alone must not make an army invincible.

The outcome of warfare should depend on multiple interacting variables, including preparation, logistics, equipment, experience, strategic decisions, geography, and economic sustainability.

### Dynamic military development

Countries must evolve as the game progresses.

Examples:

- Armies gain experience through battles and exercises.
- Poorly trained formations can improve through investment and training.
- Combat experience can contribute to tactical and doctrinal adaptation.
- Heavy losses reduce trained manpower and combat effectiveness.
- Countries can modernize outdated equipment.
- War can accelerate certain technologies and industrial investments.
- Prolonged conflict can cause exhaustion, economic damage, and recruitment problems.
- Countries can develop specialized strengths according to their circumstances.

Military size should be limited by realistic manpower, training capacity, equipment, budgets, and logistics rather than arbitrary unit caps.

AI-controlled countries and player-controlled countries must operate under the same fundamental rules.

### Real-world grounding

Use credible historical and publicly available data where practical to establish baseline country capabilities for the game's selected starting date.

Distinguish verified facts, estimates, and gameplay abstractions. Avoid invented precision or politically biased balancing.

Historical events may influence starting conditions, while subsequent in-game events should dynamically shape development.

---

## 2. LARGE AND MEANINGFUL TECHNOLOGY SYSTEM

The existing technology system needs to become significantly deeper and more useful.

I want a technology tree comparable in ambition to Hearts of Iron IV, especially for major powers, but without copying its interface or content directly.

The technology tree should include meaningful progression in areas such as:

- Infantry equipment and training.
- Armored vehicles and mechanized warfare.
- Artillery and air defense.
- Aircraft and aviation technology.
- Drones, reconnaissance, and electronic warfare where appropriate.
- Communications and command systems.
- Logistics and infrastructure.
- Manufacturing and industrial automation.
- Energy and resource extraction.
- Economic and administrative development.
- Naval technology where relevant.
- Military doctrines and strategic planning.

### Technology must have real consequences

Research should not exist merely to provide a meaningless percentage bonus.

For example:

- Better engines improve vehicle mobility but may increase cost or fuel consumption.
- Advanced armor improves protection but adds weight and production requirements.
- Improved manufacturing processes increase factory productivity.
- Better logistics allow larger armies to operate more effectively.
- Advanced communications improve coordination.
- Research into resource processing may unlock more efficient industrial production.

Technology branches should feature dependencies, specialization, competing choices, research costs, and believable development times.

### Smaller countries must remain interesting

Major countries may begin with broader industrial and scientific capabilities, but smaller countries must have opportunities to specialize.

For example, a small country could become highly competitive in drones, electronics, defensive equipment, or other focused technological fields without automatically matching a major power in every category.

Create realistic advantages, limitations, and development pathways for countries of different sizes.

---

## 3. PERFORMANCE, CODE QUALITY, AND OPTIMIZATION

Current performance appears good during my testing, and I have not noticed significant issues so far.

However, performance must remain a major consideration as the simulation becomes more complex.

Requirements:

- Preserve existing performance whenever possible.
- Avoid unnecessary calculations every frame or simulation tick.
- Optimize AI decisions and large-scale country simulations.
- Use efficient data structures and caching where appropriate.
- Avoid excessive memory allocation and duplicated calculations.
- Keep the architecture modular and maintainable.
- Detect bottlenecks before attempting major optimizations.
- Ensure that new mechanics do not cause significant slowdowns when many countries and armies are active.

Do not sacrifice gameplay depth unnecessarily for optimization, but design systems intelligently so they scale.

Use profiling and benchmarks instead of assuming code is optimized.

---

## 4. ADVANCED ECONOMY AND INTERNATIONAL TRADE

International trade and economics should become major strategic systems.

Countries should trade, negotiate, compete, cooperate, and apply economic pressure according to their interests.

### International trade

Implement or substantially improve systems for:

- Importing and exporting resources and manufactured goods.
- Negotiating trade agreements.
- Trade tariffs and customs restrictions.
- Economic sanctions and embargoes.
- Preferential trade agreements.
- Trade dependencies.
- Strategic economic partnerships.
- International market prices.
- Supply shortages and trade disruptions.
- Trade incentives and diplomatic concessions.

Trade should not always be about maximizing immediate profit.

For example, a country might accept an economically unfavorable deal to improve diplomatic relations, support an ally, secure strategic resources, or gain long-term political influence.

Likewise, countries might impose economically damaging sanctions for political or strategic reasons.

AI countries must evaluate trade agreements using economics, diplomacy, security, and long-term interests rather than accepting arbitrary proposals.

Avoid systems where unlimited money or resources can be generated through unrealistic trading exploits.

---

## 5. DEEPER ECONOMY, INDUSTRY, BUILDINGS, AND RESOURCES

I want a functioning economic simulation where infrastructure, resources, production, and military power are interconnected.

### Resources

Introduce meaningful resources such as:

- Iron ore and steel.
- Oil and fuel.
- Aluminum.
- Energy.
- Food.
- Industrial materials.
- Rare and strategic resources where relevant.

The exact resource list should be chosen according to useful gameplay mechanics, not simply expanded for the sake of complexity.

### Industrial production

Military equipment must require actual industrial capabilities and appropriate resources.

For example:

- Tanks require steel, industrial capacity, relevant components, and production time.
- Aircraft require specialized manufacturing, materials, and technology.
- Fuel shortages affect military operations.
- Damaged factories reduce production.
- Supply disruptions can stop assembly lines.
- Imports may compensate for missing domestic resources.

Buildings should serve distinct, meaningful purposes.

Examples include factories, mines, refineries, power plants, infrastructure, research facilities, military industries, and logistics centers.

### Economic development

Countries should be capable of developing their economy through investment, infrastructure, industrialization, technological improvements, and international trade.

Economic growth should involve tradeoffs:

- Civilian production versus military production.
- Short-term military spending versus long-term development.
- Domestic production versus import dependence.
- Rapid expansion versus economic stability.
- Military mobilization versus civilian workforce availability.

Everything should be connected logically.

**The economy must affect the army, the army must affect the economy, and diplomacy must influence both.**

---

## 6. IMPROVE THE MILITARY EQUIPMENT DESIGNER

The existing equipment designer and its visual schematics need significant improvement.

The current schematics are too basic, and some vehicle designs look nearly identical, particularly aircraft.

I want the equipment designer to look more professional, detailed, visually distinct, and mechanically meaningful.

### Visual improvements

- Different equipment categories must have clearly distinguishable visual silhouettes.
- Aircraft should look different depending on their intended role and configuration.
- Tanks, armored vehicles, and other categories should have appropriate proportions and recognizable differences.
- Improve schematic layouts, component positioning, and visual hierarchy.
- Introduce cleaner technical-style visuals without unnecessary graphical complexity.
- Make the designer visually attractive while remaining lightweight and optimized.

### Mechanical improvements

Vehicle components should affect the equipment's actual characteristics.

For example:

- Engines affect speed, weight, cost, and fuel usage.
- Armor affects protection, weight, and mobility.
- Weapons affect firepower, range, and ammunition requirements.
- Electronics affect detection, targeting, and coordination where appropriate.
- Different configurations should result in different battlefield roles.

There should be meaningful limitations and compromises so that players cannot create unrealistically perfect equipment.

The designer should connect directly to research, manufacturing requirements, production costs, and battlefield performance.

Avoid superficial customization where changing components barely affects gameplay.

---

## 7. MOBILIZATION, MILITARY ORGANIZATION, AND WAR PREPARATION

Expand the military management system to include realistic preparation for war.

Possible mechanics include:

- Partial and general mobilization.
- Reserve forces.
- Conscription policies.
- Military training.
- Recruitment and replacement systems.
- Army organization and command structure.
- Supply and logistical preparation.
- Equipment stockpiles.
- Defensive fortifications.
- Military readiness and deployment.
- War exhaustion.
- Demobilization after conflicts.

Mobilization must have consequences.

For example, mobilizing large portions of the population may significantly increase military manpower but reduce civilian labor availability, raise government expenditure, and damage economic stability.

An army should not be able to expand indefinitely without sufficient people, equipment, training, and financial support.

---

## 8. INTELLIGENT DIPLOMACY AND INTERNATIONAL RELATIONS

Diplomacy should become a complex, believable system rather than a collection of simple buttons.

Countries should evaluate diplomatic relationships based on their interests, security concerns, political circumstances, and historical context.

### Diplomatic features

- Peace agreements.
- Ceasefires and armistices.
- Alliances and defensive pacts.
- Military cooperation.
- Economic unions.
- Political unions and federations where conditions permit.
- Non-aggression agreements.
- Trade partnerships.
- Diplomatic pressure.
- Sanctions.
- Military assistance.
- Negotiated territorial settlements.
- International organizations and diplomatic blocs.

### AI diplomatic behavior

AI-controlled countries must behave rationally according to their circumstances.

For example:

- A threatened country might seek powerful allies.
- A large power might attempt to build a regional sphere of influence.
- Countries dependent on trade may avoid unnecessary confrontation.
- A country experiencing economic problems might pursue international agreements.
- A state suffering major military losses might become more willing to negotiate peace.

Diplomacy should include trust, strategic interests, relative power, economic dependence, previous actions, and international reputation.

Alliances should provide meaningful benefits and obligations. They should not automatically create unconditional cooperation.

Peace negotiations should consider military situations, demands, diplomatic leverage, and the willingness of the involved parties to accept terms.

**Avoid scripted behavior where countries always make the same decisions regardless of what happens in the game.**

---

## 9. COMPLETE UI/UX POLISH AND VISUAL IMPROVEMENT

The current interface still feels unfinished and needs significant refinement.

My goal is a modern, cohesive, professional strategy-game interface.

Use Hearts of Iron IV and other well-designed modern strategy games as references for information density and usability, but establish a distinct visual identity.

### Design priorities

- Clear and consistent visual hierarchy.
- Professional typography and spacing.
- Modern panels, buttons, tabs, and menus.
- Well-structured country information.
- Readable military and economic statistics.
- Improved technology tree visualization.
- Better equipment designer layouts.
- Intuitive diplomacy and trade interfaces.
- Useful tooltips that explain mechanics.
- Meaningful visual feedback for player actions.
- Clear notifications and warnings.
- Smooth, restrained transitions where appropriate.
- Consistent icons, colors, and component styling.

Avoid interfaces that feel randomly assembled, unnecessarily oversized, or visually cluttered.

The player should be able to understand complicated systems without constantly navigating through confusing menus.

The UI should feel polished while remaining fast and responsive.

---

## 10. INTELLIGENT AI AND INTERCONNECTED GAME SYSTEMS

This requirement applies to every feature above.

AI-controlled countries must use the same underlying economic, technological, military, and diplomatic mechanics as the player.

AI should understand its strategic circumstances.

For example:

- A country lacking oil should consider imports, alternative suppliers, infrastructure investments, or reduced consumption.
- A smaller country should prioritize realistic specialization instead of competing equally across every technological field.
- A country preparing for war should build appropriate stockpiles and improve readiness.
- An economically weak country should consider the costs of prolonged conflict.
- An alliance member should evaluate whether supporting another country serves its interests.

AI personalities and strategic priorities may vary, but decisions must remain grounded in the actual game state.

Difficulty should come primarily from better decision-making, not unfair hidden bonuses.

Systems must interact dynamically rather than operating independently.

### Example of interconnected gameplay

A country wants to expand its armored forces.

1. It evaluates its military requirements and manpower.
2. It researches suitable armored technology.
3. It designs or selects equipment.
4. It checks industrial capacity and required resources.
5. It imports missing materials or develops domestic production.
6. It builds the necessary factories.
7. It begins production and accumulates equipment stockpiles.
8. It recruits and trains personnel.
9. It creates military formations.
10. It maintains fuel, ammunition, and logistical support.

If any major part of this chain fails, military expansion should become more difficult, expensive, or slower.

This is the level of interconnected simulation I want throughout the game.

---

## 11. IMPLEMENTATION REQUIREMENTS

This is an existing project, not a request to create a completely new game.

Follow this development process:

### Step 1 — Analyze the existing codebase

Inspect the project structure and determine:

- Which requested systems already exist.
- How those systems currently function.
- Which parts are incomplete, unrealistic, or poorly connected.
- Which parts of the architecture can be reused.
- Which dependencies must be improved before introducing new mechanics.
- Which changes could affect performance or existing save data.

Do not assume that existing features are missing until you have inspected them.

### Step 2 — Design a coherent implementation plan

Prioritize foundational systems and dependencies.

For example, the economy, resources, production, and military equipment systems must be designed to work together.

Identify a sensible implementation order instead of adding disconnected mechanics.

Break large changes into manageable stages with clear functional outcomes.

### Step 3 — Implement actual improvements

Do not stop after presenting a plan.

Begin implementing changes in the codebase, starting with the highest-priority foundational improvements.

Preserve functional existing features wherever practical.

Avoid unnecessary complexity, duplicate logic, temporary hacks, placeholder systems, and hardcoded special-case behavior.

Prefer reusable, data-driven systems with configurable country information, technology definitions, economic rules, and AI priorities.

### Step 4 — Test and validate

After implementing each major improvement:

- Check for syntax, compilation, and runtime errors.
- Run existing tests and add appropriate new tests.
- Verify that the mechanic actually affects gameplay.
- Test how player-controlled countries interact with the system.
- Test how AI-controlled countries interact with the system.
- Check for unrealistic exploits and balancing problems.
- Verify interactions with related systems.
- Look for performance regressions.
- Ensure existing functionality still works.

Pay special attention to extreme scenarios, including prolonged wars, resource shortages, economic collapse, rapid mobilization, and countries with very different economic and military capabilities.

Do not claim a feature is tested unless it was actually tested.

### Step 5 — Polish and refine

Review both technical quality and the player's experience.

Improve confusing mechanics, weak visual elements, unnecessary UI interactions, and inefficient implementation patterns.

Keep the project maintainable for future development.

---

## 12. QUALITY STANDARDS AND FINAL EXPECTATIONS

I do not want superficial implementations that only make the game appear more complex.

I want meaningful strategic depth.

Every major feature should have:

1. A clear purpose.
2. Logical mechanics.
3. Real gameplay consequences.
4. Connections to other systems.
5. AI support.
6. Understandable player-facing information.
7. Reasonable balance.
8. Efficient implementation.
9. Appropriate testing.

Realism is extremely important, but the game should still be understandable and enjoyable.

Use realistic, evidence-based starting conditions where possible, then let the simulation develop naturally from player decisions and AI actions.

Do not predetermine which countries must win or lose conflicts. Military, diplomatic, and economic outcomes must emerge from the simulation.

If a mechanic is too complicated to simulate perfectly, develop a well-reasoned abstraction rather than implementing meaningless complexity.

### Final instruction

**Treat this as a serious long-term game development project.**

Analyze the existing implementation, establish a coherent architecture, and begin making meaningful improvements directly in the repository.

Do not redesign the entire project unnecessarily, and do not try to implement every major system as an unfinished prototype in a single pass.

Prioritize quality, realism, interconnected mechanics, intelligent AI, visual polish, and performance over the sheer number of features.

If the complete scope cannot be implemented in one development session, finish and test a coherent set of foundational improvements rather than leaving many partially implemented systems.

At the end of each implementation stage, clearly explain:

- What you changed.
- Which files and systems were modified.
- How the new mechanics work.
- What was actually tested and the results.
- What limitations remain.
- Which development stage should come next.

**The ultimate goal is a deep, believable, highly replayable, visually polished grand strategy game where every country has meaningful strengths, weaknesses, strategic choices, and opportunities for development.**