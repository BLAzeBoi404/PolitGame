# PROJECT DIRECTIVE: DEVELOP A PROFESSIONAL GRAND STRATEGY GAME

You are acting as an entire professional game development studio, combining the expertise of a senior game designer, gameplay programmer, UI/UX designer, technical artist, software architect, performance engineer, and QA tester.

Your task is to **continue developing and significantly improve my existing grand strategy game**, transforming it into a polished, sophisticated, enjoyable, and technically reliable product.

The ambition is to achieve the level of quality, depth, usability, and presentation associated with professionally developed strategy games such as **Hearts of Iron IV and Age of History 3**, while establishing an original visual identity and gameplay experience.

**Quality is the highest priority. Do not sacrifice quality, stability, depth, or usability just to finish faster.**

## 1. FIRST: UNDERSTAND THE EXISTING PROJECT

Before implementing changes:

- Thoroughly examine the entire existing codebase, project structure, game systems, assets, interfaces, and dependencies.
- Identify the current technology stack and retain it unless there is a compelling technical reason to change.
- Understand the existing gameplay systems and how they interact.
- Identify broken mechanics, unfinished features, performance bottlenecks, architectural problems, and design inconsistencies.
- Preserve functional systems and existing work wherever possible.
- Refactor outdated or poorly designed components when necessary.
- Determine which systems need to be completed, redesigned, improved, or introduced.

Do not blindly rewrite the project. Build upon what already exists.

## 2. VISUAL DESIGN AND USER INTERFACE

The game must have a modern, cohesive, polished interface comparable in quality and attention to detail to a professional grand strategy title.

Use Hearts of Iron IV as a reference for information hierarchy, military management, panels, and strategic atmosphere. Use Age of History 3 as inspiration for the simplicity and readability of a political map.

### Design requirements

- Modern, clean, sophisticated visual design.
- Consistent typography, spacing, colors, iconography, and component styling.
- Beautiful but restrained animations and transitions.
- Intuitive navigation and clearly organized interfaces.
- Professional tooltips, notifications, context menus, and information panels.
- Elegant country management, military management, technology, production, and diplomacy screens.
- Responsive layouts supporting different resolutions and aspect ratios.
- Information-dense interfaces without unnecessary visual clutter.
- Clear visual feedback for every important player action.
- Appropriate use of shortcuts, hover states, selection indicators, and contextual actions.

Avoid generic web application styling, oversized buttons, inconsistent panels, cheap-looking gradients, excessive decorative effects, and interfaces that resemble unfinished prototypes.

**The interface should feel like it was carefully designed by experienced strategy game UI/UX professionals, not automatically assembled from generic components.**

## 3. STRATEGIC MAP AND GRAPHICS

The main gameplay takes place on a detailed political map with clearly defined countries, territories, borders, and geographic regions.

The visual direction should combine the accessible map presentation of Age of History 3 with the strategic atmosphere and clarity of Hearts of Iron IV.

### Map requirements

- A clean, attractive 2D strategic map with an optional subtle sense of depth.
- Clear national, regional, and provincial borders where applicable.
- Smooth camera movement, zooming, and panning.
- Distinct country colors and visually readable territory ownership.
- Attractive country labels and appropriately scaled geographical text.
- Hover highlighting and territory selection.
- Multiple useful map modes, such as political, diplomatic, military, economic, infrastructure, and resources.
- Army positions, movement paths, battle indicators, and front lines.
- Consistent rendering at different zoom levels.
- Efficient rendering of potentially thousands of geographic regions and military objects.
- Clear visual distinction between selected, allied, neutral, and hostile territories.

The graphics should remain relatively simple and lightweight, but they must look intentional, professional, and visually appealing.

Do not attempt to compensate for weak design with unnecessary visual complexity.

## 4. DEEP MILITARY SYSTEM AND VEHICLE DESIGNER

One of the most important features is an advanced but intuitive military equipment development system.

Players should be able to research, design, customize, improve, manufacture, and deploy military equipment.

### Vehicle categories

Implement or expand systems for:

- Tanks and armored vehicles.
- Infantry fighting vehicles and personnel carriers.
- Military trucks and support vehicles.
- Artillery and specialized ground equipment.
- Fighter aircraft.
- Bombers and attack aircraft.
- Transport and reconnaissance aircraft.
- Additional equipment categories when they fit the game's design.

### Modular equipment designer

Create a visually appealing equipment design interface inspired by the depth of Hearts of Iron IV's vehicle designers, while keeping the interaction straightforward and approachable.

Allow players to select compatible components and build equipment variants.

Depending on the equipment category, components may include:

- Engines and propulsion systems.
- Armor configurations.
- Main and secondary weapons.
- Turrets and chassis.
- Suspension and mobility systems.
- Aircraft frames.
- Fuel capacity and efficiency.
- Electronics, targeting, and detection systems.
- Reliability and support modules.
- Specialized modules.

Every component should have understandable gameplay consequences.

For example, improving tank armor might increase protection while also increasing weight, cost, and fuel consumption.

Increasing engine power might improve mobility at the expense of fuel consumption and manufacturing costs.

Show real-time changes in equipment statistics, including:

- Attack and defense.
- Armor and penetration.
- Speed and mobility.
- Range and fuel consumption.
- Reliability.
- Production cost.
- Maintenance requirements.
- Weight and other relevant characteristics.

The design system must use internally consistent formulas and meaningful trade-offs rather than arbitrary statistics.

### Production and upgrades

Players should be able to:

- Research new technologies and components.
- Save and name custom equipment designs.
- Compare different equipment variants.
- Create production lines.
- Assign industrial capacity.
- Upgrade existing equipment when feasible.
- Replace obsolete designs.
- Manage production efficiency and resource consumption.
- Equip military formations with appropriate equipment.
- Understand why equipment is unavailable or cannot be produced.

The entire system should be both strategically interesting and visually satisfying to use.

## 5. CORE GRAND STRATEGY GAMEPLAY

Develop the game into a coherent strategy experience rather than a collection of disconnected features.

Core systems should include, where appropriate:

### Countries and politics

- Country selection and management.
- National characteristics and modifiers.
- Political stability and government systems.
- Diplomacy, alliances, relations, and treaties.
- War declarations and peace negotiations.
- National decisions and events.

### Economy and industry

- Resources and economic development.
- Civilian and military industry.
- Construction and infrastructure.
- Equipment production.
- Trade and resource availability.
- Economic and industrial limitations.

### Technology and research

- Meaningful research progression.
- Technology prerequisites.
- Equipment unlocks.
- Military and industrial improvements.
- Clearly presented research trees.
- Balanced progression and understandable benefits.

### Military and warfare

- Recruitment and army organization.
- Division or formation management.
- Equipment allocation.
- Movement across the map.
- Front lines and strategic orders.
- Combat resolution.
- Supply, logistics, and attrition.
- Terrain and weather effects where appropriate.
- Strategic military AI.

Implement these mechanics as connected systems with logical dependencies.

Avoid superficial features that exist only as decorative interface elements.

## 6. GAMEPLAY DESIGN AND BALANCING

The game needs to be enjoyable, understandable, and strategically meaningful.

- Establish a clear and satisfying gameplay loop.
- Make choices have visible consequences.
- Balance accessibility with strategic depth.
- Avoid unnecessary micromanagement.
- Give players meaningful long-term goals.
- Introduce mechanics progressively through contextual guidance.
- Ensure economic, military, research, and diplomatic systems interact logically.
- Avoid dominant strategies caused by poorly balanced statistics.
- Make progression rewarding without becoming repetitive.
- Create intelligent and believable behavior for AI-controlled countries.

Prefer fewer deeply integrated and functional mechanics over many incomplete or disconnected features.

## 7. TECHNICAL QUALITY AND OPTIMIZATION

Treat performance and reliability as essential development requirements.

The game should operate smoothly even when the world contains many territories, countries, armies, production lines, and simultaneous calculations.

Pay particular attention to:

- Efficient map rendering.
- Reduced unnecessary redraws.
- Efficient simulation updates.
- Memory usage and cleanup.
- Data structures suitable for large strategic simulations.
- Separating rendering logic from game simulation.
- Appropriate caching.
- Efficient pathfinding.
- Avoiding unnecessary repeated calculations.
- Performance under accelerated game speed.
- Save/load performance and data integrity.
- Avoiding interface freezes during expensive operations.

Use profiling and measured results to identify bottlenecks rather than making unsupported assumptions.

Design scalable systems that remain maintainable as new content and mechanics are introduced.

## 8. USER EXPERIENCE AND ACCESSIBILITY

The player should never have to fight against the interface.

- Make important actions discoverable.
- Provide informative tooltips and concise explanations.
- Clearly communicate prerequisites and restrictions.
- Allow easy navigation between connected systems.
- Maintain consistent interaction patterns.
- Provide undo or confirmation mechanisms where appropriate.
- Support configurable controls and useful keyboard shortcuts.
- Make text readable at common screen resolutions.
- Ensure panels behave correctly at different window sizes.
- Preserve smooth and predictable interaction during gameplay.

Every screen should have a clear purpose and should be tested from the perspective of someone actually playing the game.

## 9. QUALITY ASSURANCE AND TESTING

Do not assume a feature works simply because the code compiles.

For every implemented system:

1. Verify the implementation.
2. Run available automated tests.
3. Test normal gameplay scenarios.
4. Check relevant edge cases.
5. Test interactions with existing systems.
6. Identify and fix errors.
7. Verify performance where applicable.
8. Confirm that the interface behaves as intended.

Add or improve automated tests for critical game logic.

Test important scenarios such as:

- Country selection and switching.
- Territory selection and map interaction.
- Army recruitment and movement.
- Equipment design and customization.
- Technology research and unlocking.
- Production and resource consumption.
- Combat calculations.
- Save and load operations.
- Long-running simulation stability.
- Different window sizes and resolutions.

Never claim something has been fully tested unless testing was actually performed.

If visual or runtime testing is unavailable, explicitly distinguish verified results from unverified assumptions.

## 10. DEVELOPMENT STANDARDS

Follow professional software engineering practices.

- Use clean, modular, maintainable code.
- Separate game logic, rendering, interface, and data management.
- Avoid unnecessarily complex abstractions.
- Reuse common components appropriately.
- Eliminate duplicated logic.
- Use consistent naming and conventions.
- Add meaningful error handling.
- Avoid hardcoded values where configurable data is more appropriate.
- Keep project structure logical and scalable.
- Document important architectural decisions.
- Do not introduce unnecessary dependencies.
- Maintain compatibility with the existing project unless changes are justified.

Use original UI elements and assets, or properly licensed resources. Existing commercial games are design references, not sources to copy proprietary artwork, code, or content.

## 11. IMPLEMENTATION WORKFLOW

**This is an implementation task, not merely a request for suggestions, concepts, or documentation.**

Follow this workflow:

### Phase 1: Technical and gameplay audit
Examine the project and identify its current state, major problems, and highest-impact improvements.

### Phase 2: Architecture and visual foundation
Improve project architecture where necessary and establish a coherent, professional visual design system.

### Phase 3: Core gameplay
Implement and improve essential map interactions, country management, progression, economy, and military systems.

### Phase 4: Advanced systems
Develop the modular equipment designer, research, production, logistics, warfare, and AI behavior.

### Phase 5: Integration and refinement
Connect the systems, improve usability, refine visuals, fix inconsistent behavior, and add missing interactions.

### Phase 6: Optimization and validation
Profile, optimize, test, fix defects, and verify the integrated gameplay experience.

These phases should guide development, not prevent useful improvements to existing systems.

Work directly with the real project files whenever the environment permits it.

Avoid spending the entire effort on planning. Make meaningful, working code changes and verify them.

If the entire scope cannot be completed in a single development session, preserve the project's working state, document completed work and remaining priorities, and continue from that state in the next session.

Do not present placeholders, mock interfaces, or incomplete mechanics as finished features.

## 12. DESIGN AUTHORITY AND DECISION-MAKING

You have permission to make professional technical, architectural, gameplay, and visual design decisions when requirements are not fully specified.

Do not ask unnecessary questions about small implementation details.

Choose solutions based on:

1. Overall game quality.
2. Long-term maintainability.
3. Gameplay depth and coherence.
4. User experience.
5. Visual polish.
6. Performance and reliability.
7. Compatibility with existing systems.

When multiple implementations are possible, choose the approach that delivers the strongest overall result, not simply the fastest or easiest one.

Do not add random complexity merely to make the project larger.

Every major feature should serve a meaningful gameplay or usability purpose.

## 13. EXPECTED RESULTS

The expected result is a substantially improved, playable grand strategy game with:

- A beautiful, professional strategic map.
- A modern and cohesive interface.
- Intuitive country and military management.
- Meaningful economic and political systems.
- A sophisticated modular vehicle designer.
- Functional equipment research, upgrades, and production.
- Integrated military and strategic gameplay.
- Reliable, tested systems.
- Well-optimized performance.
- Clean, maintainable architecture.
- A consistent visual and gameplay identity.

The final result should not feel like a student project, a collection of UI demonstrations, or a quickly assembled prototype.

It should feel like a carefully developed strategy game created by a competent professional development team.

## FINAL DIRECTIVE

**Do not prioritize speed over quality.**

Do not take shortcuts simply to produce a visible result.

Do not replace functional gameplay with decorative interface elements.

Do not sacrifice performance for visual effects that add little value.

Do not ignore existing bugs or incomplete systems.

Think critically about the design and implementation. Challenge weak solutions, improve systems when justified, and validate your work.

Focus on building a game that is enjoyable to play, satisfying to interact with, visually polished, technically stable, and capable of growing into a full-featured grand strategy experience.

**Start by inspecting the existing project, determine its current state, establish the highest-priority improvements, and then begin implementing them.**