# Changelog

<!--
  Placeholders, not release history. The entries below record the file's shape — headings newest
  first, `## <version> — <title>` — and nothing about what shipped. `D47.Core.csproj` embeds this
  file as the `D47.Core.Changelog` resource for the About dialog; nothing else parses it.
-->

## 1.27.0 — Stock stories begin

When the carrier's tritium warning fires and the stored plan for that carrier is past its last restock stop, the warning now names up to three pristine waypoints with an icy ring, nearest first, and the ships you have fitted for mining, or says none is.

Plotting a carrier route now also says where tritium is sold within one jump (500 light years) of the destination. A station that sells it is named with how long ago it was reported. If only carriers list it, the answer says so, at carrier prices, and that those listings move; if nothing does, it says that. A one-way plot with no station supply adds "Plot it as a round trip and carry the whole total." Asking for the nearest station selling a commodity, when no station does, now searches again with carriers and labels the result "carriers only, at carrier prices". The Galaxy search entry under Privacy and egress now says a carrier plot asks api.ardent-insight.com where tritium is sold around the destination.

Asking which of your ships are fit for mining lists the ones carrying a mining laser or Volley Repeater, a collector controller and a refinery, naming those modules. Ships missing one are named with what they lack, such as "no refinery", and a ship whose fitted modules were never read is reported as not seen fitted.

The Navigation tab's Plan page has a Carrier Route card for your own fleet carrier. Fill in To; From defaults to the carrier's system, and "Come back to the start" is on by default. There are no fields for the tank, hold tritium or used capacity: they are read from the carrier, as with "plot my carrier to Colonia". The plotted route is headed "Carrier route", with the total tritium and when carrier management was last read, and lists each jump with its distance, the tritium it uses and what is left in the tank, marking restock stops with the tonnes needed and systems with a pristine icy ring. A carrier whose management panel has never been opened gets "Open carrier management once so I can read the hold." on the card. Fleet › Carrier has a "Plan a carrier route" button that opens the card with From set to the carrier's system.

A planner on the Plan page that answers without plotting, such as a refusal or "No route from…", now keeps that answer on its card. It was cleared as soon as it appeared.

A stored carrier plan now follows your carrier. It moves forward when the carrier's own location or jump event reaches the next system, matched on the carrier's id. Your own ship jumping through a waypoint system, and a squadron carrier arriving there, do not move it. The plan can say how many jumps remain and whether the next stop needs a tritium restock.

D47 can now hold reminders for a moment in the game: your next docking, docking at a named station, arriving in a named system, docking at your own carrier, an empty or full hold, a full material, or your next session. Each is said once when the journal reaches that moment, as one sentence about the moment followed by your own words exactly as you gave them: "You asked me to remind you when you docked. Buy limpets before you leave." Your words are never sent to a language model. There is no way yet to set one by voice. Reminders are kept per Commander in `data\journal-reminders.json`, and one that has been said is removed at the next game load. Settings > Callouts has a new switch, Reminders, on by default; with no reminders set it says nothing.

Saying "plot my carrier to Colonia" plots your own fleet carrier's jumps there and back, using the tank, the tritium in its hold and the used capacity from the last time carrier management was opened. The answer gives the number of jumps, the tritium for the whole journey, up to three restock stops by name and a count of the rest, how many waypoints have a pristine icy ring to mine from, and how old the management reading is. Say "one way" to leave out the trip back. A squadron carrier, or a carrier whose management panel has never been opened, is refused. The Galaxy search entry under Privacy and egress now says a carrier plot sends the start, the destination, those three figures and the carrier's capacity to spansh.co.uk.

Saying "explain that" or "explain that event" asks the language model about the event selected on the Journal page. The event goes to the model as Elite wrote it, except that the text of any message another player typed, or that you typed, is replaced with "[withheld: typed by a player]"; who sent it, on which channel and when are still sent. Where the Journal page has a paragraph for that kind of event, it is sent after the event, so the spoken answer and the paragraph agree. With nothing selected, the answer is "Nothing is selected on the Journal page." and nothing is sent. The Language model entry under Privacy and egress now says this.

The Journal page's reading now has a WHAT THIS CHANGED band between What this means and Every field, open by default, saying what d47 took from the selected event, such as "This event updated: docked at BNH-T2F". An event that changed nothing reads "Nothing in d47's picture changed.", which is a normal answer for most events, and an event that never reached your state has no band. Folded, the band shows the receipt as a one-line preview, and selecting another event opens it again.

Pledged to a Power, Directive 47 now warns when you start a hyperspace jump with salvage you scooped in your own Power's system and have not handed in, because it earns reinforcement merits only when handed in at a Power contact in that same system. It names up to three kinds and counts the rest, and says nothing about escape pods, which earn no merits. The new Powerplay salvage row under Callouts turns the warning off.

Pledged to a Power, Directive 47 now says which way the merit modifier leans the first time you enter normal space in one of its systems in a session: reduced merits for reinforcing your own Power's quiet system, better pay once rivals have undermined it, fifteen percent more for undermining a rival's. It says nothing in an unoccupied system. Asking "what's the merit multiplier here" is answered with the figures, with the galaxy map's marked-system bonuses given as "if" because the journal does not say which systems are marked. The new Powerplay merits row under Callouts turns the spoken line off.

The Set aside button on a goal is now Remove. Below the goals, a Removed list names each removed goal with a Recover button that puts it back where it was, and is hidden when nothing is removed. By voice, "remove the mercenary goal" and "recover the mercenary goal" do the same. Goals you had already set aside appear in the Removed list.

ElevenLabs, OpenAI, Cartesia and Edge speech now starts playing while the rest of it is still arriving, so the first words of an answer come sooner. This applies to a voice with no treatment on it and to the stock COVAS voice with its reverb, which now starts sooner on ElevenLabs as well. Voices heard over comms, including a story character on a link, now start the same way, so a hailed NPC answers sooner. The link's level control now adjusts as the voice plays instead of measuring the whole line first, so loudness can shift slightly within a line; the Guardian helmet and hologram effects, which use the same link, change the same way. A voice with Guardian effects still plays once the whole sentence group has arrived, as before.

Ticking Goals on the Checklist page now replaces the list with the goals, which fill the page below the bar and scroll only when they do not fit. The filter, Delete completed items, Suggestions and Add are hidden while Goals is ticked, and unticking it brings the list back with the same filter, search and selection. Each rank goal shows two labelled bars: one to the top of its ladder, "To Elite V" (or "To King" and "To Admiral" for the navies), and one to the next rank, so Elite IV at 56% in Trade shows 96% and 56%. Where the rank is known and the percent is not, the second reads "not known yet". Other goals keep one unlabelled bar.

The five career goals are now named Elite V in Combat, Elite V in Trade, Elite V in Exploration, Elite V as a Mercenary and Elite V in Exobiology, and their progress counts the percent into your current rank: Trade rank 12 at 56% reads "Elite IV, 56% into it" and is 12.56 of 13. Elite ranks now say their percent, except Elite V. The Imperial Navy and Federal Navy goals now finish at rank 14, King and Admiral, where before they finished at Prince and Vice Admiral.

The Journal page's right pane now reads the selected event instead of showing its JSON. It shows the event's sentence, the time and how long ago, a row for each thing the event says, a paragraph on what the event is, and Every field, folded, which lists every field as Elite wrote it and can be selected and copied. A value decoded from a game token is underlined with dots and shows the token on hover; a row label explains its field on hover. An engineer, or a ship you still own, opens its page on the Engineers or Ships tab. A row with more than six values shows six and a "+4 more" tile. The system you are in is drawn in cyan, a warning such as "You are wanted here." in red, and another player's message in grey exactly as typed. Below about 480 pixels wide, labels sit above their values.

What this means opens the first time you select an event of a given kind after d47 starts, and is folded for every later event of that kind, with a one-line preview beside it. You can still open or fold it by hand. Nothing is saved, so every kind opens once more after a restart.

A ship's own page under Asset Mgmt › Ships now shows its hull in 3D, in place of the still, once the hull's mesh has arrived and Hull pictures is on. Drag to turn it, Shift+drag to roll, right-drag to pan, use the wheel to zoom, and double-click to return to the starting view. Once you have clicked it, the arrow keys turn it, Shift+Left and Shift+Right roll it, Ctrl+arrows pan, + and − or Page Up and Page Down zoom, and Home returns it to the starting view. The strip above it has a LIGHT level from 0 to 2, a ↺ button that resets the view, BESIDE and WIDE, and a □ button that opens it over the whole window at the same view. Escape closes the whole window and keeps the view. The hull is drawn in the theme's own colours. A hull with no mesh, and the headset panel, keep the still.

The engineer unlock lines for nine on-foot tributes now show how many of the items you are carrying. Baltanos, Domino Green, Eleanor Bresa, Hero Ferrari, Jude Navarro, Kit Fowler, Rosa Dayette, Terra Velasquez and Wellington Beck each ask for items from the ship locker, and the line reads "carrying 3 of 5" from your backpack and locker together. It stays undecided even when you carry enough, because the game writes nothing when you hand the items over; the line is met once the engineer is unlocked.

Asking how to get a ship or module now finds the nearest station that sells it at any distance, where before it looked only within 50 light years. For a module or ship sold at 20 stations or fewer in the whole galaxy it adds the count: "Nearest: Abel Laboratory (Arque), 98 ly. Only 6 stations are reported to sell it." With none it says "No station is reported to sell it." Other station searches keep their 50 light year default.

The Narrator now speaks every thirty to sixty minutes with a stock core aboard, the same as with any other core, instead of every five to ten. It no longer works tips on using D47 or on playing Elite into its narration, and the two "Tips" switches under Narrator in Plan and story callouts are gone. A settings file that still holds them loads as before.

While you are pledged and D47 has read your rank, the model now also knows the perks that rank holds and whose territory you are in, so it can weigh them when you ask "what should I do here" or "where should I sell this". For example: "Powerplay perks at rank 8, in Li Yong-Rui's territory: rebuy -40%, exploration data sales +20%. This system is Li Yong-Rui's." A rival's system is named, and a system with no controlling Power says so. Nothing is added at rank 0 or for a Power D47 has no table for.

With Hull pictures on, D47 now also fetches a hull's 3D mesh when you open the ship, from the same GitHub release and under the same setting. The mesh is up to 20 MB a hull, so the large hull files for the whole fleet come to about 850 MB. Privacy and egress names the mesh; the destination and what is sent are unchanged.

You can now ask what a Powerplay rank gives: "what does my next Powerplay rank give me" or "what would Mahon give me". D47 says the perks and rebuy reductions held at that rank, up to three of the modules unlocked and a count of the rest, what the next rank gives, and the next perk and the first module with their ranks. For your own Power each rank also says how many merits away it is, once D47 has read your merits: "Rank 9, 1,708 merits away, gives a mini care package." Another Power is answered at rank 100 unless you name a rank, without merits. With no pledge and no Power named, D47 asks you to name one. The help site has a new page, Powerplay ranks.

Asking how to get a Powerplay module now names the rank that unlocks it. If you are pledged and below that rank, D47 says so: "Li Yong-Rui unlocks it at rank 34; you are rank 8, 201,708 merits short." The merits part is left out until D47 has read your merits. At or past the rank it says "Li Yong-Rui unlocks it at rank 34, and you are rank 40." With no pledge it says "Every Power unlocks it; Li Yong-Rui soonest, at rank 34." Other items are answered as before.

The button that opens a tab's own settings now reads "Page settings" instead of "Settings for this page", on Asset Mgmt › Ships, Asset Mgmt › Carrier, Stories and the other tabs that have one. The arrow and the count are unchanged.

The conversation now labels the ship's AI's turns with its name instead of "D47". That is the name you gave your ship, or the core's own name (COVAS, Warden, Cora and so on). It applies to replies, callouts, error cards and proposal cards, on the Conversation page and the flat page. A turn keeps the name it was written under when you change the core or the ship name; new turns use the new name. The line a newly selected core speaks appears under that core's name. Carrier, Tower, Crew, Comms, Narrator and named characters keep their labels.

Your carrier captain now warns when your own carrier's balance covers less than four weeks of upkeep. It speaks when you dock at the carrier and when you plot a jump, giving the weeks covered and the weekly figure: "Sacred Fire's account covers three more weeks of upkeep at 9,700,000 a week." Where upkeep has been taken off the recorded balance since it was read, it says so. It speaks once for each recorded balance, and again after the game is loaded. It is silent until d47 has a weekly upkeep figure, and for squadron carriers and carriers you do not own. Settings > Callouts has a new switch, Carrier upkeep, on by default; "stop warning me about carrier upkeep" turns it off.

Your carrier captain now warns when your own carrier's tritium is less than two full jumps at its current load. It speaks when you dock at the carrier and when you plot a jump, naming the tonnes aboard and what a full jump burns: "Sacred Fire has 150 tonnes of tritium; a full jump at this load burns 88." Where the carrier has jumped since the last reading, it adds how old the reading is and that the tank can only be lower. It speaks once for each tritium reading, and again after the game is loaded. Squadron carriers and carriers you do not own are not announced. Settings > Callouts has a new switch, Carrier fuel, on by default; "stop warning me about carrier fuel" turns it off.

The help site has a new page, Journal events, in General help. It has a short paragraph for each of the 45 commonest journal events: when Elite writes the event, and what d47 does with it.

The missions D47 gives the model as game state now follow the same order as the spoken mission board: expiring within the hour first, then handed in at the station you are docked at, then the rest. Before, it named the three that expire soonest, so a mission you could hand in where you were docked could go unnamed. A mission past its expiry is named after the live ones, and a delivery says how far it has got, for example "12 of 20 delivered".

When you ask D47 about a particular mission, or about more than the three most urgent, it can now read your whole mission board. Before, it saw only three missions and guessed at the rest, so a question about your seventh mission got no real answer. Each mission shows its faction, cargo, destination, how far it has got, time left and reward. The spoken mission board still names three.

Suit and weapon builds now belong to one Commander. Before, every Commander on the same installation saw the same builds on the Suits and Weapons pages, and the Materials gap counted every Commander's ship and on-foot plans. Each Commander now sees and counts only their own. Builds you made before this change go to the Commander whose kit holds that suit or weapon. Builds for something no Commander holds now, and planned builds, go to the Commander who got most of the others. If none match, or two tie, they go to the first Commander d47 sees, so a single-Commander installation loses nothing.

Settings > Stories has a new switch, Story ratings, on by default. Off, nothing is fetched or sent. D47's Privacy and egress list has a matching Story ratings entry naming the address and saying what a vote carries: the story, your stars and a random number made on this PC for your Commander, and no Frontier ID, Commander name, key, position or journal content. Each card on the Stories page now shows its average rating as stars in the theme's accent, with the vote count beside them, or five empty stars and "(No ratings yet)". The filter bar has a Rating choice, from Any rating to 4 stars & up, and a Sort choice, Catalogue order or Highest rated; Clear filters resets both. A story's page shows its average under the title, and for a story you have picked, Your rating: click a star to rate it, click the same star again to take your rating back, or use Left, Right and Delete. A rating that could not be sent says "Your rating is saved and will be sent next time.", and is sent the next time the Stories page opens.

The Materials page has a new sidebar item, FARMING › Farming route. It lists the known farming sites nearest to you first. Each site is a card showing its kind, the material, the system and body with the surface coordinates, and the distance in light years. When you are in a site's system, that system is cyan and the distance reads HERE. Each system name has a copy button. Under each card are how to collect the material, whether a relog respawns it, and what it trades down to at a material trader. The ALL / RAW / MANUFACTURED / ENCODED control above the cards shows only that kind of site. The list is redrawn when you change system.

The Materials page has a new sidebar group, ON FOOT, with Backpack and Ship locker. Each lists what you hold, grouped as Items, Components, Data and Consumables, with each group's total. Backpack totals read "4 HELD", since the game gives no backpack cap. Ship locker totals read against the locker's cap, "647 / 1,000"; Consumables read "66 HELD · CAP 100 EACH", because their cap is per item. A tile shows NEED and a number when a live plan is still short of that resource. Choosing a tile, or an on-foot line on Needed by plans, opens a page for that resource: its kind, how many are in the backpack, the ship locker count against its cap with a gauge, the plans that need it, the settlement buildings and containers it is found in, and the Bartender swaps you can pay for with what you hold.

The Ships page has a new COMPARE button, beside Plan a ship you do not own. It opens Compare ships, a table of every ship as it was when you last flew it: cargo, jump range, unladen mass, fuel, value, rebuy and where the ship is. ORDER BY sorts the table by jump range or by cargo, largest first, and the sorted column's heading is orange with ▼. MINIMUM CARGO steps through ANY, 16, 32, 64, 128, 256 and 512 T and hides ships with less cargo space. The title reads "SHOWING 3 OF 7". Ships in your current system are cyan. A ship d47 has not seen you fly shows dashes for its figures, and any minimum cargo hides it. When no ship meets the minimum, the page says "No ship carries that much cargo." The table is redrawn when you board a ship, refit one or change system.

The Fleet tab has a new page, Stored modules, after Ships. It lists every module you have in storage, grouped by the system it is stored in: the system where the game last listed them first, then the others by transfer time. Each group's heading names the system, cyan when you are in it, then the station where known and the number of modules. Each row shows the module, its class such as 5A or 7E, the transfer cost, FREE for a module already where you are, and the transfer time as hours and minutes. The search box, "Filter by module name", hides modules that do not match and any system left with none, and says "No stored module matches “…”." when nothing does. The list updates when the game next lists your stored modules, which it does when you open Outfitting.

Asking for your sampling progress now says how far apart samples of a genus must be and how many metres are still to go, or that you are far enough. It uses a distance per genus from community sources, since Frontier publishes none. For a genus without a figure it says what it said before: how far you have moved and the closest gap the game has accepted from you.

The Navigation tab has a new last page, ON THIS BODY, in the window and the headset. It names the body you are on, or the one you last sampled, in cyan, with its biological signal count, genera, other signals and gravity. A body you have not mapped also shows what mapping it would pay. Under SAMPLING are your three samples of the genus in progress and the distance since the last one, which follows the game's position as you walk. Before the genus's colony distance the number is white and the line reads KEEP WALKING with the metres to go; at or past it the number turns blue, the line reads FAR ENOUGH and the next sample reads READY. For a genus with no colony distance the page shows only the distance and says the colony distance is not known. Away from the surface, where the game gives no position, it says so. ON THIS BODY lists each genus as done, sampling or not started, with the species' value once it is known.

The Navigation tab has a new page after ON THIS BODY, UNSOLD DATA, in the window and the headset. It shows the organic data and the exploration data you are carrying and have not sold, and the two together. For organic data it shows the estimated value, the number of species, how many have the first footfall bonus, and how many have no price and are left out of the total. For exploration data it shows the estimated value, the number of bodies, how many were mapped efficiently, and how many have no price. Each has a RESET button that asks first and then counts that total from zero. Afterwards the line beside the button gives the time of the reset, and it still gives it after a restart. D47's language model cannot reset either total. You can, from this page or by saying “reset unsold exobiology” or “reset unsold exploration”.

On the Stories tab, the copy button after a system name is now level with the text it follows, and so is the full stop after it. It sat about half a line low on the story card, the story's Next line and each told beat's trigger line.

The Narrator's default voice, for anyone who has not chosen one, is now a voice with an American, British, English, Scottish, Welsh, Irish, Australian, New Zealand or Canadian accent. A voice described as narration is preferred; if there is none, the first voice with one of those accents is used; if the voice list has no such voice, the Narrator keeps the voice it had before. A Narrator voice you picked yourself is kept.

The COVAS core's voice, where d47 chose it, is now a calm British woman: a female British voice described as calm if the voice list has one, otherwise a female British voice, otherwise a female voice. A COVAS voice you picked yourself is kept. An installation whose COVAS voice d47 chose is re-cast once, the next time voices are paired.

The Commander tab has a new first page, STANDING, in the window and the headset. It shows your reputation with the Empire, the Federation, the Alliance and the Independents, each with its band and a number from −100 to 100 on a gauge that fills from a zero mark in the centre: orange to the right for a positive number, red to the left for a negative one. Negative numbers and the Unfriendly and Hostile bands are red. A power the journal has not reported yet shows a dash and an empty gauge. Below that are your Imperial and Federal navy ranks, each with its rank number out of 14, how far you are towards the next rank, and a gauge. The page redraws when the journal reports a change, and the line along its bottom says when the journal last moved it on.

The Commander tab has a second page, STATISTICS, in the window and the headset. A list down the left names every section of the career statistics the game writes when you log in, in the game's order, each with how many figures it holds. Choosing one shows all its figures in a grid of three columns. The search box, "Find a figure in any section", finds figures across every section by their name or their section's name, and lists each with its section and value; choosing one opens its section and empties the search. When nothing matches it says "No figure in any section matches that." Asking for your career statistics reads each figure by the same name the page shows, and the Material_Trader_Stats section is now called Material trader.

The Commander tab has a third page, THIS SESSION, in the window and the headset. At the top right is what you have earned since the game started, the total of every source. Below it are your time played, jumps, distance, bodies scanned, materials gained, interdictions, deaths and the balance you started with. EARNINGS BY SOURCE lists bounties, combat bonds, trade, exploration, missions and vouchers, each with a gauge measured against the largest; a source that has paid nothing is grey. The session starts again by itself each time the game loads, so the page has no reset. Before the game has loaded it says the session has not started.

Asset Mgmt › Materials now opens on NEEDED BY PLANS, with a list down the left. Each ship, suit and weapon with a plan has its own group: the name, then the hull and number of plans, or "Suit" or "Weapon" and the planned grade, and how many materials are short. Each row shows the material, its ledger (RAW, MANUFACTURED, ENCODED or ON FOOT), held and needed, how many are short or ✓ MET in blue, and a gauge. Each build is measured against everything you hold, so two builds that share a material can each read ✓ MET while together they are short. The notes about engineer ranks, slots with no material total and slots costed at the most expensive module are still above the groups. The Ship and On foot cards, which listed every material you hold, are gone for now. An on-foot row opens nothing yet; its detail page comes in a later release. The page redraws when your plans or your inventory change.

The Materials page's list now has a SHIP MATERIALS group: RAW, MANUFACTURED, ENCODED, GUARDIAN and THARGOID, each with how many materials it holds. Choosing one shows every material in it, held or not, in groups by grade headed with the grade and its storage cap, such as GRADE 1 and CAP 300. Each material is a tile with its name, how many you hold, and a yellow bar showing how close that is to the cap; the count turns yellow at the cap. When a plan needs the material, NEED and the total all plans need sit before the count. Guardian and Thargoid materials appear only in their own ledgers, grouped by the game's category and then grade, such as MANUFACTURED · GRADE 3.

Choosing a material on a Materials ledger, or a ship material on NEEDED BY PLANS, opens a page for it. Its top line, MATERIALS › RAW ›, takes you back to the Materials page or to that ledger. The page shows the material's ledger, category, grade, and how many you hold against the cap with a gauge. NEEDED BY PLANS lists each plan that needs it, with how many and how many are short, and appears only when a plan needs it. HOW TO FARM IT says where to get it. For a raw material or one from high-grade emissions, NEAREST TO YOU searches the galaxy when the page opens and again when you change system, and lists the places nearest first with their distance. With “Look things up in the galaxy” off it says to turn that on instead. AT A MATERIAL TRADER shows what to give for this material: trading up from the grade below, down from the grade above, and across from another category at the same grade. A Guardian or Thargoid material says that no material trader deals in it.

The panel tabs are renamed and reordered: TRANSCRIPT, STORIES, COMMANDER, ASSET MGMT, NAVIGATION, SETTINGS. Adventures is now Stories, Fleet is now Asset Mgmt and Routing is now Navigation, in the window and the headset. Saying "stories", "assets" or "navigation", or "open the stories tab", opens that tab. Saying "fleet" never opened the Fleet tab, because the voice matched the tab's internal name, Loadout. Saying the name of a page on any tab, such as "suits", now opens that tab on that page from wherever you are; a tab's own name still wins, and a word two tabs' pages share opens nothing. If d47 was closed on one of the renamed tabs, it reopens on that tab under its new name.

When the window is too narrow for every tab's name on one row, every tab shows a small picture instead of its name, all at once, rather than wrapping onto a second row. Hover over one to see its name; the page title still names the open tab. Widen the window and the names come back. With tabs down the left, the names always show.

HELP and the PRE-RELEASE badge are now in the window's title bar. The badge sits right after the version number, and HELP sits just before the minimise button. The tab row holds only the tabs. In the headset, HELP and the badge stay at the end of the tab row.

Engineers is no longer a tab of its own. It is the last page of Asset Mgmt, whose pages now read SHIPS, SUITS, CARRIER, MATERIALS, ENGINEERS, in the window, the headset and the overlay. The Route is no longer a second page beside the directory: press Route on the Engineers page to open it, and Back returns to the directory. Saying "engineers" from any tab still opens it. If d47 was closed on the Engineers tab, it reopens on its default tab.

The Checklist tab is now the Commander tab, and the checklist is its first page, in the window, the headset and the overlay. Suggestions and Activities still open from the checklist. Saying "checklist" from any tab opens the checklist, and saying "commander" opens the tab. The Open the checklist button in Settings opens Commander on the checklist. If d47 was closed on the Checklist tab, it reopens on Commander.

The Course and Community Goal pages are gone from the Navigation tab, which now reads PLAN, BOOKMARKS, MARKET, TRADE ROUTE. Setting a course by voice, the Community Goal tools and the sale callout work as before. Saying "refresh" no longer reruns the Community Goal search, because it only worked while that page was open; "community goal search" still runs it. The Inara API key is now in Outside sources on the Settings page, and the week-boundary day and hour are under Community goal sales in Plan and story callouts. If d47 was closed on either page, it reopens on Plan.

Progress is no longer one of the Navigation tab's pages. The tab opens on Plan with Progress beside it, and the breadcrumb reads Plan › Progress. A PROGRESS button at the top of Plan opens it again, including when galaxy lookups are off. Showing a plan result puts the result in Progress's place, and pressing PROGRESS swaps it back. Back, the breadcrumb or pressing the tab again leaves Plan alone until d47 next starts. Progress now shows the system you are in and the destination as its title, the distance to go at the right, and each hop in a row with its number, star class and one tag: HERE, SUPERCHARGE HERE, NO SCOOP or SCOOP UNKNOWN. If d47 was closed on Progress, it reopens on Plan with Progress beside it.

The Bookmarks page has an Add button. It bookmarks what you have targeted in Elite, as saying "bookmark this" does, and asks for a name first; leave it empty to use Elite's name. If nothing is targeted, the page says so and makes no bookmark.

A system name on the Adventures and Bookmarks pages now has a copy button beside it. On Adventures it follows the system in a story's next step and in each beat already told, whether or not you are in that system; the name is still cyan only when it is your current system. On Bookmarks each row has one for its system. Clicking it copies the name and does not open the story.

Home system is now set per Commander, like the Character sheet. Each Commander has their own, and the arrival callout and the answer to what d47 is watching for use the home of the Commander who is flying. A home system you had already set stays as the starting value every Commander reads until they set their own.

COVAS now reports flatly. While it is aboard, Core humor is ignored, no line it speaks carries delivery notes such as `[sighs]`, and every announcement, such as a promotion or a notable kill, is said exactly as written without being reworded by the model. Its intro and return lines are said as written too. Questions you ask still go to the model, which is told COVAS is equipment and expresses no emotion. The Guardian cores are unchanged.

A story can now open with fixed lines, spoken when you pick it and before anything else of the story. Each line is said in its speaker's voice and posted to Messages under the story's title, then the narrated beacon scan follows where the story has one, and chapter one's first line comes after both. The lines can name you as Commander and follow the gender set for your Commander, or say "they" when none is set. They are spoken once per pick: resuming a paused story does not repeat them, and picking the story again does. Stories published without an opening start as before.

A story chapter's lines given to a cast member are now written as that person's own words, in the first person. Before, narration that described the dock hand or the caller was tagged with their id and read out in their voice. Description of a place, the ship or what a person left behind goes to the narrator, and every line is written plainly, without a closing aphorism or the story's own premise repeated. A cast line that names its own speaker is sent back for one rewrite, and if it still does, the narrator reads it and the chapter is kept. Chapters already written keep their lines.

The Narrator no longer speaks as itself in a story chapter. A line the Narrator reads, including a ship line while a stock core is aboard and the chapter's opening, is told in the third person from outside the cockpit: no "I", "me" or "we", no speaking to you, and no offering, asking or waiting, such as "Standing by". A Narrator line in the first person is sent back for one rewrite, and if it still is, it stays and the chapter is kept. Cast lines are unchanged.

A chapter beat can now hold up to three lines, each with its own speaker, spoken in order. A cast member can say their own line and the narrator can then describe the scene in the same beat, where before the whole beat went to one speaker. Each line is spoken in its speaker's voice and posted to Messages under that speaker's name and picture, and the ship says where to go next after the last line when that line is not the ship's. The Adventures page shows every line of a beat with its speaker. Most beats still have one line, and stories saved before this change load unchanged.

Avatar clips now play forward to their last frame and then backward to their first, repeatedly. Before, each clip jumped back to its first frame at the end of every loop, which showed as a visible jump.

Every line d47 speaks is now on the Transcript. Before, only the callouts spoken from the main queue were: the narrated beacon scan when you pick a story, a story ending and the lines after you answer it, the Guardian cores waking line, a lore search result, an autonomous action's line, and a keyboard action's acknowledgement were spoken with no Transcript line. In-game comms and chatter you only overheard are on the Transcript too, under the sender's name; chatter you could not answer still cannot be answered. A story ending written for the Narrator is now spoken in the Narrator's voice instead of the ship's.

A limit on how far a station may be from its star, in the commodity, Market page, Community Goal and trade searches, is now applied by Spansh instead of only afterwards on this PC. Spansh ignored the old form, so pages were filled with distant stations that were then dropped, and a search could return fewer stations than exist in range.

Kokoro's Nicole voice (`af_nicole`) is a whisper, so it is no longer chosen for a core, the carrier captain, the tower or the Narrator. Voices for other ships and stations on comms can still be given it, and you can still pick it by hand. A core that was given Nicole automatically gets another voice the next time the voices are paired; one you picked yourself keeps it.

In the headset, the mini panel's avatar is now a square the panel's height inside its rail, showing the whole picture. Before, it could be drawn several times larger than the rail and cropped to part of the face, and its size changed during a session.

A ship picture that finishes downloading after you leave its page and come back is now put in, instead of the page staying without it until it is rebuilt. A ship picture built for a page you never opened is no longer kept in memory.

Asking for bodies that are not landable no longer returns only landable ones. The body search now treats "landable: no" as not asking about landing at all.

**Chatterbox** is a second voice that runs on this computer, offered wherever Kokoro is. It runs Chatterbox Turbo on the processor, needs no key and sends nothing anywhere, and speaks in one of the 12 shipped reference voices. Its model is 691 MB, downloaded once from huggingface.co with **Download it** on the **Chatterbox voice** row, which appears while any slot uses Chatterbox. It performs directions such as `[laugh]` and `[sigh]` instead of reading them out. Pronunciation corrections apply to Kokoro only, there is no speaking rate, and every line carries Resemble's inaudible Perth watermark. **Privacy and egress** names the download under **Speech model download**.

**Your voice**, under Voice and hearing in Settings, records you reading one sentence aloud so a story can cast lines in your own voice through Chatterbox. **Record** starts it, and **Stop** or 7 seconds ends it; a take under 5 seconds, or one with nothing louder than -40 dBFS, is refused with the reason. **Play** plays it back and **Delete** removes it. There is no way to import a clip. The recording is kept in `data\voice\own.bin`, encrypted for your Windows user, and never sent anywhere; **Privacy and egress** lists it as **Your recorded voice**. No voice picker offers it, and every line spoken in it carries the Perth watermark.

Every line of a stock story is spoken by the speaker it names. A line for the ship is spoken by the core aboard in the ship's voice, or by the Narrator while stock COVAS is aboard. A line for the narrator is spoken in the narrator voice. A line for one of the story's characters is spoken in the Kokoro or Chatterbox voice the story gives that character, whatever **Where each voice comes from** says, through the character's own comms link and effects and none of yours. This covers clues, finale lines, the narrated beacon scan and each chapter's lines, and a chapter can now give a line to a character, such as a mayday caller. Each line is posted to Messages from the character's name, with their picture. The Narrator setting no longer decides who speaks a clue.

A story whose characters need a voice that is not on this PC cannot be picked. Its page lists what is missing and disables **Pick**: the Chatterbox download and its size, the Kokoro download and its size, or a recording under **Your voice**. The ship posts the same list to Messages if you try anyway, and the download rows under **Its voice** appear while a downloaded story needs them. If a running story loses a voice, for example because you delete the recording, the story pauses, the message is posted again, and **Resume** is refused until the voice is back.

A story's card page has a **Cast** section listing its main characters, before you pick it and while it runs. Each row shows the character's picture, name and voice, with **Play sample**, **Change voice**, **Change picture** and **Use the default**. A character in two versions appears once you have said whether your Commander is a man or a woman. **Change voice** also appears on every message from a story character, main or not. It offers every voice provider, then every voice that provider lists, of either gender, with the story's own voice marked; the story's text is not rewritten to match. **Use the default** puts the story's voice back. A paid provider's lines count in the speech spend, and a story whose chosen provider has no key stored lists the key among what it needs before **Pick** is enabled. If the chosen voice fails a sentence, the story's own voice speaks it and the character's row says why. **Privacy and egress** names each character you put on a hosted provider, with its story and the provider. A story's main characters are now always named on its card, and none speaks in your own voice.

A message that was spoken as it was posted, such as a story beat, clue, nudge, beacon scan or ending, keeps the audio that was played. Open it in **Messages** and press **Play** to hear the same audio again, captioned with its text; it is not synthesised again, so a paid voice is not billed twice. Pressing **Play** on another message stops the first. The clips are kept in `data\messages\`, one per message, and are deleted with their message. A clip spoken in your own voice is encrypted for your Windows user like the recording, and deleting the recording deletes those clips and keeps the messages' text.

Every panel tab has the same header. Under the tabs, a title line names the tab, gives the page's one-line summary, such as how many engineers you have unlocked, and holds the page's search field and controls, with an orange rule beneath it. The face of the ship's AI is a 104-pixel square at the right of the tabs and the title line. Pages no longer repeat the tab's name as their own title, and a page you have opened from another, such as one engineer, has a smaller title under the breadcrumb.

The mini panel, on the desktop overlay and in the headset, shows the face of the ship's AI at its left edge, as tall as the panel, with the messages beside it at the width they had before. It opens at 792 by 280 instead of 512 by 280. The headset mini panel is 0.526 metres wide instead of 0.34, so its text looks the same size; a width you set yourself is kept. The overlay animates the face, and the headset shows the still frame for the current state.

The face on the panel fetches the clips of the core aboard that it does not have yet, from a GitHub release into `datavatar-clips`, at startup and when you switch core. **Avatar animations** under Persona turns it off, and **Privacy and egress** lists it.

The face on the panel plays a short looping clip for the core aboard in each state, such as listening or thinking, when that clip is in `datavatar-clips`. A state with no clip shows the usual mark, and your own pictures in `datavatar` still come first. The headset panel shows the first frame of the clip and does not animate it.

A story card on the Stories page shows a picture of each of its main characters below the line naming its length, level and core. A character that comes in two versions shows the one for your Commander's gender, and none until you have said which. The list fetches these pictures with it; the rest of a story's pictures come when you pick it. **Privacy and egress** says so. **Change picture** on a message changes the card's picture too.

Stock stories are downloaded from a GitHub release into `data\stories`: the list when the Stories page first opens in a session, and a story's hidden layer and cast pictures when you pick it, with **Downloading** on the button until they land. A story already running is fetched at startup if its file is missing, and fetches any cast picture added to the release since it was downloaded. **Download stock stories** in the Adventures settings turns it off, and **Privacy and egress** lists it.

Stock stories are downloaded when you open the Stories page, instead of shipping with the app.

Opening a story's page now downloads its hidden layer and cast pictures if they are not on disk, and the page redraws with its **Cast** section when they land, without picking the story first. Before, a story you had not picked showed its cast in the list but no **Cast** section on its page. **Download stock stories** off still fetches nothing, and **Privacy and egress** now says a story's files are requested when you open its page.

A message from a story character with a picture shows it above the text. **Change picture** replaces it with a PNG, JPEG, BMP or WebP file of your own, kept in `data\pictures` and shown on every message from that character; **Use the default** brings the story's picture back. **Privacy and egress** lists **Pictures you chose**: kept on this PC, never sent.

The Stories page filters stock stories by length and by the Commander they were written for.

Opening one story or message after another replaces the one that was open instead of stacking them, and opening a page that is already open goes back to it. D47 no longer crashes when you click back to a story you just read.

With a stock core aboard, a narration can work in one tip on using D47, told as something your Commander had not yet learned. Each feature's tip is said once per Commander. **Tips on using D47** under Narrator in Plan and story callouts turns them off.

During your first 50 hours of play, a narration with a stock core aboard can also work in one tip on playing Elite, such as fuel scooping after a jump, told in narration from a short hand-written list. Each tip is said once per Commander, and a tip on using D47 comes first. **Tips on playing Elite** under Narrator in Plan and story callouts turns them off.

Abandoning a story, or switching to another, removes the messages it posted. A finished story keeps its messages, and so does an adventure you wrote yourself.

While a stock story is running and switched on, the missions you take are tied to it. Each mission gets one short aside from the story's public layer, in the first of three places: the line the core aboard says when you take it, the faction's mission scene chatter, or a narration during a lull. The aside never changes the mission's name, giver, destination, cargo, reward or deadline. Taking a mission now gets a line even when d47 has no cargo or market fact to give, as long as a language model and personality are on. The chapter writer is told the missions you hold, and may make one's destination an arrive or dock beat. A paused or switched-off story leaves missions alone.

A stock story keeps only its current chapter and the one before it on the Adventures page. Earlier chapters move to `data\story-chapters.jsonl` as each new chapter begins, and all of a story's chapters move there when it is abandoned, switched or finished. Story chapters no longer count toward the 40 adventures the Adventures page holds, so a long story keeps writing chapters, and the chapter writer is given the last ten earlier chapters by premise and a count of the rest. Your log still lists the beats of archived chapters.

The Adventures tab has a **Stories** button, opening the stock stories, each written to run for the length on its card.
The catalog holds twenty-three stories in this release, **My Own Mayday**, **Ride Along**, **Black Box**, **The Borrowed Callsign**, **The Far Side**, **Journeyman**, **Good Standing**, **The Omphalos**, **The Scholarship**, **The Hillside**, **One Star**, **Runner-Up**, **The Milk Run**, **The Long Hour**, **Standing Order**, **Next of Kin**, **Honest Work**, **Dark Horse**, **Point of Order**, **Blind Charter**, **The Right Seat**, **Old Light** and **Clean Bill**, and more are added to it one at a time. **Journeyman** is written for a new commander and works through each of the game's main activities in turn. **Good Standing** is written for a mid-range commander who joins a cooperative of independent pilots. **The Omphalos** is written for an endgame commander with a fleet carrier, and spends a year crossing the galaxy in search of Raxxla. **The Scholarship** is written for a new commander who runs from home in a Sidewinder built from scrap and takes a pilots' charity's scholarship. **The Hillside** is written for an endgame commander with a fleet carrier, and runs for 3 days: a mining settlement in LHS 145 under siege, with seventy-two hours of shield left. **One Star** is written for an endgame commander with a fleet carrier, and runs for 1 week: a famous critic books passage to review ten sights. **Runner-Up** is written for an endgame commander with a fleet carrier, and runs for 2 weeks: one last contest with the rival who has beaten you for twenty years. **The Milk Run** is written for an endgame commander with a fleet carrier, and runs for 1 month: one last run on your mother's route of fifty years, in the month she has left. **The Long Hour** is written for an endgame commander with a fleet carrier, and runs for 3 months: a war citation for an hour you cannot remember. **Standing Order** is written for an endgame commander with a fleet carrier, and runs for 6 months: you park over a poor mining outpost, clear its raiders and stay, because somebody needs you. **Next of Kin** is written for an endgame commander with a fleet carrier, and runs for a year: your rival's will makes you guardian of the daughter you never knew she had, and someone is hunting her. **Honest Work** is written for a mid-range commander, and runs for 1 month: you washed out of the Navy academy and have written to your father as a lieutenant ever since, and the Guardian core you bring aboard corrects every untrue message your ship sends. **Dark Horse** is written for a mid-range commander, and runs for 2 weeks: you come home from a Guardian beacon to find your name on the ballot for Pilots' Steward at Ray Gateway, as the joke candidate, with the vote in eleven days. **Point of Order** is written for a mid-range commander, and runs for a year: you win a seat on the Eranin Assembly as a joke, and the Guardian core you bring aboard reads every one of its rules. **Blind Charter** is written for a mid-range commander, and runs for 3 days: you wake with nine hours missing, by your own choice, and an Imperial contract recovery agent at the foot of your ramp asking where you took her. **The Right Seat** is written for a mid-range commander, and runs for 3 months: since a drive fault at the rings of Uszaa 2 you have not carried a passenger, and the girl who sat in the front row that day has asked for you as her flight instructor. **Old Light** is written for a mid-range commander, and runs for 6 months: you fly an instrument out ahead of the light of a sixty-one-year-old night and watch a settlement called Lantern burn, a year closer with every light year. **Clean Bill** is written for a mid-range commander, and runs for 1 week: something came aboard with a cheap load of Thargoid salvage, and a hazard officer on comms gives you seven days to find it.
The list shows each story's commander level (new, mid-range or endgame, a guide and not a limit) and a
blurb saying why you might pick it, and a story's page adds the level's guideline, the tone, the story in your words and why it sends you to a Guardian beacon. **Pick** makes the story's words your Backstory and has the
ship's AI write chapter one, which begins at once and ends when you scan the Guardian beacon nearest to
you with the data-link scanner; arriving in the beacon's system is not enough. The chapter's last line
is said before the Guardian core wakes, in the voice that was aboard. When a chapter finishes, the next is written and begins. **Switch** abandons the running story and
picks another, and **Abandon** ends it. Each story has a hidden
layer D47 never shows; **Privacy and egress** says it is sent to the language model with each chapter.

A generated adventure or story chapter no longer sends you to a system that needs a permit, such as
Shinrarta Dezhra, unless you are already in it.

A story chapter that asks for an illegal mission now tries to keep you clean. The chapter's writer is given up to five Anarchy systems near you, and a mission beat for a crime (`Mission_OnFoot_Heist`, `Mission_OnFoot_Sabotage`, or any family with `Illegal` in its name) must come directly after an arrive or dock beat in one of them, with a line telling you to take a mission whose target is run by an Anarchy faction. When none is near, the writer is told to use no illegal mission. While such a beat is current, approaching the target settlement of a live mission of that family, if a faction other than Anarchy runs it, makes d47 say once per mission that the job is a crime there. Counting is unchanged: any completion of the family counts.

The Commander's log lists each adventure and story beat you reached in its range, naming the story and chapter for a story, sourced to the journal event that fired it. The Goals page shows your current story with its clues given out of the total, its stage and its current beat, with a note when it is paused or switched off. A finished story shows as done until you set it aside, and an abandoned one is not shown.

A story chapter is now written to its story's genre and to your Commander. The writer is given the genre's three Save the Cat elements, the jump range of the ship you are in and your credits at the last load, and sizes each chapter to finish in one to three play sessions, or one session in a story shorter than three months, carrying on a longer undertaking such as saving for a ship. From chapter two on, no more than two of a chapter's beats may be travel (arrive, dock, land or scan). With 250,000,000 credits at the last load, or a Caspian Explorer, a chapter of a story three months or longer may go as far as Colonia or Sagittarius A*. Every third chapter after the beacon scan must contain a beat of the activity your statistics show you have done least: bounties, combat bonds, mining, organics, rescues or passenger missions.

A story chapter can now ask you to take part in a war, civil war or election, or to work missions for a faction. A war or civil war counts the combat kill bonds you earn for one side of an active or pending conflict in the system you are in; an election counts missions for one side that raise its influence in the election's system. The side is the one your first contribution went to, and the other side's do not count. Taking part is enough: the beat does not need your side to win. When you next arrive in a system where a conflict you took part in has ended, d47 notes whether your side won, and the next chapter is told. For a faction beat, the next chapter is told whether that faction's influence rose between your visits. The chapter writer is shown the conflicts d47 has seen in systems you have visited.

A story chapter now leaves you a credit reserve. A beat that buys a ship you do not own, a fleet carrier or a squadron is written only when your credits at the last load cover the price plus a reserve of the price again or 500,000,000, whichever is less; a fixed threshold above that still applies, and founding a squadron now needs 20,000,000. The writer is told the most it may ask you to spend, so saving up and buying fall in separate chapters.

A story's finale now ends at one place. Its first finale chapter names a landable body, which may be many hops away: up to five hops at the chapter's reach for each finale chapter after the first, and at least five. Each later finale chapter is told where it is, how far away and how many chapters are left, and a middle finale chapter that starts more than one reach from the destination must end closer to it than it started. The last finale chapter ends by landing on that body, and that landing may be farther than one hop. The destination is kept when d47 restarts.

A story chapter's current beat now has a **Not for me** button, on the chapter's page and on the Stories page, and a voice command, "this beat is not for me". It asks "Write a different beat?" and, on yes, the ship's AI writes new beats from the current one to the end of the chapter. Beats already done stay, and the new ones count only what you do after refusing. The activity is remembered for the rest of the story: no later chapter asks for it, a refused mission family still lets other families through, and the Stories page lists what you have refused. A beat that names a place is replaced and not remembered, and the Guardian beacon scan that ends act one cannot be refused. The model cannot refuse a beat for you.

A stock story now runs to its length and ends. A length is 3 days, 1 week, 2 weeks, 1 month, 3 months,
6 months or 1 year; **The Hillside** runs for 3 days, **One Star** for 1 week, **Runner-Up** and **Dark Horse** for 2 weeks, **The Milk Run** and **Honest Work** for 1 month, **The Long Hour** and **The Right Seat** for 3 months, **Standing Order** for 6 months, and every other story in the catalog for a year. The Stories list shows each story's
length before its commander level, and a story's page shows it beside the level's guideline. A year's
hidden layer gives up fourteen clues, 7, 14, 21 and 28
days after you scan the beacon and then every 30 days to day 330, with paused days not counted. Each
clue also waits for a new play session and for a chapter finished since the last one, so a Commander
back from months away gets the next clue, not several. The chapters follow the story's beat sheet by
the clues you have had. After day 360 and the fourteenth clue comes a four-chapter finale, with a clue
as each chapter begins, and after the fourth the story is finished: no chapter is written, the Stories
page marks it **Finished**, and it no longer holds back any Guardian core. Act one now ends at the beacon
only once the ship you are in can reach it, with a fleet carrier, or within 20 jumps with a fuel scoop
fitted; until then each chapter stays within a session's flying and works toward a ship that can make
the trip. A story of 3 days, 1 week or 2 weeks has no trip to a beacon: at **Pick** the beacon scan
is narrated in the story's own words, posted to Messages from its speaker and said, the story's core then
comes aboard and says its waking line, and chapter one begins with no beacon beat. Its days count from
the pick, and a real beacon scan in any system during the story wakes the Heretic. While stock COVAS is aboard the Narrator speaks every clue, with narration on
or off; with any other core aboard the Narrator speaks a clue when narration is on, and otherwise the
core does. Each is also posted to Messages, from whoever spoke it. The Narrator, invented chatter, scene
chatter, the chapter writer and any core but COVAS now all read the same hidden layer and hint at it
without stating it. A running story is enough for the Narrator to narrate a lull, with no character
sheet, Backstory or scenario set.

When a story is finished, its ending is written in the same voice as the clues, posted to Messages and said, with the story's
options listed under it. Press an option on the message, or say "accept the ending" when there is one, or "choose ending one" to
"choose ending four". Your choice is kept on the story, its closing line is said and posted, and an option that brings a Guardian
core aboard has that core say its waking line. No option takes a core away. The ending waits for your answer across restarts, and the
language model cannot answer it.

A story
chapter's page no longer shows its premise, turn or ending, and has no **Edit**, **Remove** or **Write the next chapter** button; **Abandon** is the way to stop one, and **Begin again** resumes it. **Remove** on any adventure now asks before it deletes.

Removing an adventure, or a story leaving the catalog, now clears its messages. An abandoned adventure or story keeps them.

You can pause a running story. Clear **Story on** on the Stories page or the mini panel, or say "pause the
story", and no beat is said, no clue or nudge is owed, nothing from the story is posted to Messages and
the story's hidden layer is left out of what the Narrator and the core say. The clue clock stops.
Narration and chatter carry on as before, and your Backstory is unchanged. A place
you visit while the story is off does not count; its beat waits for the next visit. The switch lasts
across restarts. Say "resume the story" or tick **Story on** to bring it back.

Stories need Elite Dangerous: Odyssey. When your last session ran without it, the Stories page says
so, **Pick** and **Switch** are disabled, a running story writes no chapter and gives no clue, and the
days do not count toward a clue. The story carries on from where it was at the next session with
Odyssey. A story you switched off stays off.

A running story now holds the Guardian cores back until you scan a Guardian beacon while it runs,
and COVAS speaks meanwhile. The first beacon brings back every core but the Heretic, and a beacon in a
second system brings back the Heretic; each is said once per story, aloud and in Messages. Pausing the
story, switching it off or abandoning it gives the cores back at once. Your chosen core stays chosen and
is aboard again when the hold lifts. With no story running, every Guardian core can be chosen on every
install, and `data/guardian-cores.json` is no longer read. While the hold lasts the Persona picker
shows COVAS, and choosing a held core is refused with a reason that names the story.

Each stock story is written for one Guardian core, shown on the Stories list and on the story's page.
When you scan the story's first beacon, that core comes aboard in place of the one you had, says the
waking line, and Messages says it came aboard and that you can choose another in Settings. A core you
choose afterwards stays, and pausing, abandoning or finishing the story leaves the core aboard.

A story can write one of its characters in two versions, one for a Commander who is a man and one for
a Commander who is a woman, each with its own name. For such a story, the story's page asks **Your
Commander is**: **A man** or **A woman**, and **Pick** and **Switch** wait until you choose. The same
control is on your running story's page, and a change there applies from the story's next line.

The local voice says "story", "stories", "storyline" and "backstory" with the vowel of "or", not "stawry".

COVAS now speaks with a short room reverb, like the cockpit voice in Elite. The voice itself is
unchanged. To turn it off, untick **COVAS reverb** under **COVAS Voice** on the Its voice page (an
Advanced setting); **Test** beside it plays a stand-in voice through the reverb at no cost. Guardian
cores, your own cores and the Narrator are not affected.

While COVAS is aboard it makes no idle remarks: no ambient line, no opening line, and no word when you
get into or leave a game. The Narrator speaks in those moments instead, every five to ten minutes by
the In Ship chatter timing, narrating the moment whether or not a story is running, and naming you
from the journal when there is no character sheet. Turning the Narrator or personality off leaves those
moments silent. With any other core aboard, nothing changes.

You can answer a narration. For ninety seconds after the Narrator's last line, whatever you say is
answered by the Narrator with more narration, in the third person and the past tense, up to four
replies. A command d47 runs or offers without the model, such as "gear down", still goes to the ship,
and so does anything that starts with your ship AI's name. Each reply is posted to Messages from the
Narrator.

A story beat can now wait for something done a number of times: bounties collected, kill bonds earned,
missions completed, tons of a commodity sold, or tons refined by mining. Only what you do after the beat
before it counts, and the story's card shows the running total, such as "Kill bonds for LTT 7786 Labour:
3 of 8". Skimmer massacres, Disable, Hack, Scan and damaged-station missions are not used or counted yet.

A story beat can also wait for exploration, on-foot and other activities done a number of times: stepping out
on foot, collecting items, analysing organic samples, mapping bodies, surveying signals such as Thargoid or a
ring hotspot, landing at a wreck, logging codex entries, selling exploration or organic data, salvaging cargo,
dropping into a signal source, rescuing, launching the SRV and hiring crew. A beat can also wait for an engineer
to invite or unlock you (an on-foot engineer is seen only at your next login), for a new suit or weapon mod to appear in your suit loadout, for a change to your ship's livery, for your fleet carrier to jump to a new system, to buy a fleet carrier, to join a wing or another Commander's crew, or to join or found a squadron. A chapter only asks for the carrier and squadron kinds where you can do them: a fleet carrier at 7,000,000,000 credits or more at the last load and none owned, founding a squadron at 10,000,000 and none joined.
Suit mods and livery are seen when the game next writes the loadout, so a beat may fire some minutes after the change. The story's card shows the running total.

The Callouts setting "A jump is long after" is now labelled "Hyperspace Tunnel Delay (in seconds)", and its help reads "Measured from entering hyperspace rather than from starting the jump." The value, its default of 30 and the saved setting are unchanged.

The Callouts toggle for remarks on a slow hyperspace jump is now "Hyperspace tunnel delays" instead of "Long jumps", so you say "stop calling out hyperspace tunnel delays" to turn it off; "stop calling out long jumps" no longer matches. The settings blurb and the "what are you watching for" list use the same name, the list showing `hyperspace-tunnel-delay: on`.

The Callouts setting "Report route progress every" is now labelled "Report route progress every N jumps", so the label says what the number counts. Its help no longer opens with "In jumps." The value, its default of 3 and the saved setting are unchanged.

The Callouts setting "Call out biology from" is now labelled "Call out biology from (credits)", so the label says what the number is. Its help no longer opens with "In credits:". The value, its default of 10,000,000 and the saved setting are unchanged.

The Lore setting "How often a system's lore is worth repeating" is now labelled "How often a system's lore is worth repeating (days)", so the label says what the number is. Its help no longer opens with "In days,". The value, its default of 7 and the saved setting are unchanged.

The Narrator settings "The least time between narrations" and "The most time between narrations" are now labelled with "(seconds)", so the labels say what the numbers are. Their help no longer opens with "In seconds.". The values, their defaults of 1800 and 3600 and the saved settings are unchanged.

The Callouts settings "The least time between In Ship chatter", "The most time between In Ship chatter", "The least time between NPC chatter" and "The most time between NPC chatter" are now labelled with "(seconds)", so the labels say what the numbers are. Their help no longer opens with "In seconds,". The values, their defaults and the saved settings are unchanged.

The Callouts setting "Trading Mode needs a hold of" is now labelled with "(tonnes)", so the label says what the number is. Its help no longer opens with "In tonnes,". The value, its default of 25 and the saved setting are unchanged.

A story's dock beats now go only to stations where the ship can land. The station search's small, medium and large pad counts decide it, so a medium ship is no longer sent to a station with only small pads. A story that stays in the current ship uses that ship's pad; otherwise it uses the smallest pad of any ship you own. A station whose pad counts are unknown is still allowed.

A story's dock beats no longer go to a settlement in a system with a war or civil war, active or pending, because its docks may be closed for fighting on the ground. Starports and outposts in that system are still allowed, and an election does not count. Whether the system is at war comes from whichever is newer: your own journal's last reading of the system, or the galaxy search's record of it.

When you request docking at the station a story chapter is sending you to and the station answers that its docks are offline, d47 says once that the docks there are offline and writes a different beat in that one's place, without being asked. The new beat does not name that station, and when it is written d47 says where to go next. Docking stays allowed for the rest of the story, and a second denial at the same station while the beat is written says nothing more. This uses one call to the language model.

The same now happens for an adventure you asked for that is not part of a story. When its current beat sends you to a station whose docks are offline, d47 says so, writes a different beat from that one on, avoiding that station, and says where to go next. Beats already done stay as they were. With a story and an adventure both under way, only the one sent to the closed station is written again.

A system name that follows a station or body name, as in "dock at Wolff's Haven in LTT 7786", is now always spoken as the name. Before, a repeat of the system in that position was spoken as "it", which read as the station. Other repeats still become "it".

A generated adventure or story chapter no longer has a beat that tells you to arrive at the system you are already in, whether you are there when the chapter starts or the beat before it took you there. Such a beat could not be completed until you jumped out and back or logged in again. d47 now has the language model write a different beat in its place.

Stories and adventures now call each step the Commander flies to an objective, not a beat. The Stories, Adventures and Messages pages, the story tools and the Adventure objectives callout setting say so, and so does what d47 and its language model say about the story. "This objective is not for me" and "give me a different objective" refuse the current objective; "this beat is not for me" and "give me a different beat" still work. Saved adventures are unchanged.

When a generated story or adventure sends you to arrive in a system, dock, land or scan, it now says why, in one sentence after where to go: "Next: dock at Sellings Holdings in HIP 97950. A comms specialist there reads signals like the burst." Asked why you are going there, d47 answers from that sentence rather than saying it has no reason. The person a reason names is someone you hear about, not someone you meet. Objectives written before this change have no reason and are said as before.

## 1.26.0 — Adventures run in chapters

A finished adventure has a **Write the next chapter** button on its page in the Adventures tab. It
opens the same form as **Ask for one**, and the ship's AI writes a story that continues from how the
finished one turned and ended. It reads the finished adventure in full, including what was said as
you flew it, and each chapter before that by name and premise only. The draft card says which
adventure the new one follows. **Privacy and egress** now says that asking for an adventure sends
your location, ships and ranks, and that a next chapter also sends the story it follows.

When an adventure beat that turns the story is said (its function is a midpoint, a turn or "all is
lost"), D47 adds one plain line: your Backstory still describes where the story began, and it is yours to
change. It is said only when you have a Backstory and only if the beat itself was said. Nothing writes to
your Backstory. **Callouts** has a **Backstory reminder** checkbox, on by default.

An adventure beat can now wait for you to **board a ship**: it fires when you buy that ship or swap into
it. You can add one in the adventure editor, or write `"kind": "board"` with a `shipType` in the file.
The ship's AI writes a board beat only for a ship your brief names.

## 1.25.0 — Messages and the sleeping cores

The Adventures tab has a **Messages** button. Every adventure beat D47 speaks is also kept there as a
written message, newest first, with unread ones in bold and the unread count on the button. Opening a
message marks it read. The messages are kept in `data/messages.json`, up to 200.

A new install now starts on **COVAS**, a plain factory ship AI with no Guardian history. COVAS speaks
without the Guardian Voice Effects, whatever is ticked there.

When an adventure has waited at its next beat through three play sessions and seven days, the
**Narrator**'s next narration leans toward where it waits, with at most one such nudge per adventure
each time D47 runs. The nudge is also posted to Messages, from the narrator, and appears in the
adventure's story feed.

## 1.24.0 — A Narrator tells your story

A new voice, the **Narrator**, now tells your Commander's story during a lull, in the third person
and the past tense, from your character sheet, backstory and current scenario, connected to where you
are now. It waits for a quiet moment: never in supercruise, never during a fight, and never straight
after other chatter. It says nothing while all three texts are empty. Its lines are captioned
*Narrator* and spoken in a voice that is never your ship's. The rows are Settings › Callouts ›
Plans and stories › **Narrator**, on by default, with the least and most time between narrations
(thirty and sixty minutes); its voice is Settings › Voice and hearing › Its voice › **Narrator voice**. Each narration is written by the language model
and sends your whole backstory; with no model, or personality off, there is no narration, and Privacy
and egress now says when the Narrator sends your texts.

With a current scenario set, the people of a settlement you are on foot at are now heard on their own
radio reacting to what just happened: your arrival, being found, each killing, your death and your
escape, at most one exchange every 45 seconds. They are given your scenario whatever *Who knows about
it* is set to, and do not know who you are or why you are there. The row is Settings › Callouts ›
Chatter and messages › **Scene chatter**, on by default. Each exchange is written by the language
model; with no model, or personality off, there are no scenes, and Privacy and egress says when scenes
send your scenario.

Scene chatter now reaches ship fights too. With a current scenario set, the pilots around you are heard
on their own channel when you drop into a conflict zone, an extraction site or pirate activity, when a
pilot interdicts you, when the shooting starts, when you destroy a ship, when you are destroyed and when
you escape into supercruise. At a conflict zone they may fly for either side. While a ship scene is
heard, the bystanders' combat exchange after a kill is not made, so a kill is answered once; with no
scenario it is made as before.

Scene chatter now answers missions as well. With a current scenario set, taking, completing, failing or
abandoning a mission can be answered on comms by people of the faction that gave it. Missions of the
same kind within a minute of each other are answered once, naming up to three and counting the rest.
The model is told to say nothing unless the missions bear on your scenario, so most unrelated missions
are not answered. A failed or abandoned mission is answered only when D47 knows which faction gave it.
At a settlement, the exchange is now written knowing your missions to that settlement or against its
owner, so a raid can carry the reason for it. The people there still do not know about those missions.

Typing into a text setting, such as Backstory, and then opening another settings page no longer
closes D47, and what you typed is saved.

D47 now keeps track of your active missions, and knows them after a restart: for each mission in
the list Elite writes when you log in, it finds the mission's acceptance in your older journals and
reads its destination, faction, cargo and reward from there. A redirected destination and cargo
delivered so far are kept too. The game state sent to the language model with each question now says
how many missions are active, what they pay in total, and where the three that expire soonest go and
how long each has left. A mission accepted
before any journal still on disk is known by its internal name only, with no destination or reward.

You can now ask for your mission board: "mission board", "what missions do I have", "read my missions" or
"what am I hauling". D47 answers itself with no model, naming up to three missions with their
destination and time left, then how many more there are and the total reward. Missions expiring within
the hour come first, then those handed in at the station you are docked at, then the rest by soonest
expiry. Asking "what missions should I take" adds that D47 only sees missions after you accept them,
because the journal does not list what a station offers.

D47 now speaks about missions without being asked. Docking at a station where missions hand in says
how many and what they pay together, for example "Two missions conclude here. 2.9 million waiting."
Undocking from it with one still unclaimed says "You're leaving with a hand-in unclaimed". Each
mission is announced an hour before it expires and again ten minutes before, once each; a mission
already inside the hour when D47 starts gets only the warning still ahead. The row is Settings ›
Callouts › Plans and stories › **Mission hand-ins and expiry**, on by default.

When a mission's work is done and its hand-in moves, D47 now says so: "That's Courier to Sol done. Hand-in
moved to Jameson Memorial in Sol. Say plot it to set the course." Saying "plot it", "plot the hand-in" or
"set course for the hand-in" then plots that system, or copies it to the clipboard when auto-plot is off.
The offer stands until another redirect replaces it or the mission leaves the board. Nothing is plotted
until you say so.

When you accept a mission, D47 now checks it against your ship and the station. A mission for more cargo
than your hold carries says so: "That's 99 tons against a 64-ton hold. Two trips, or a bigger ship." A collect
mission whose commodity the station you are docked at has in stock says "They sell Power Generators here."
Delivery missions, which hand you their cargo, get no such line. The market check uses the market D47 last
saw at that station. Both lines are part of Settings › Callouts › Plans and stories › **Mission hand-ins
and expiry**.

Accepting a delivery or collect mission now adds a line to your checklist, such as "Deliver 99 Polymers
to Crown Barracks, Wadjuk". It shows how many have been delivered, or before the first delivery how many
are in your hold, and ticks itself when the last one is handed over. You cannot tick it by hand. It
leaves the list when the mission is completed, failed or abandoned. Courier and passenger missions get
no line.

When you accept a mission with an expiry, D47 also works out whether you can reach its destination in time. It
asks the galaxy search for the distance from where you are, divides by your ship's jump range, and allows five
minutes per jump. If that is longer than the time left, it says "Wadjuk is about 40 jumps. That's tight for a
12-hour deadline." It says nothing when the trip fits, when the distance cannot be found, or when Galaxy search
is off. The line is part of **Mission hand-ins and expiry**.

The session report now includes the influence Elite reported on your completed missions, per faction, in
Frontier's `+` notation with the direction from the trend: "Influence from missions: Party of Yoru up ++ x4,
+ x1; Tirada Jet Comms Limited down + x2." At most five factions are named, most marks first. The logbook
records the same line as one mission fact.

## 1.23.1 — Delivery direction can be a sound

With a voice that performs direction (ElevenLabs v4 Turbo or v3), D47 is now told it may open a
sentence with a sound as well as a manner, such as a klaxon blaring, static crackle or a door slamming.
A note with a comma in it, or up to sixty characters long, is now recognised as direction, so it is
kept out of captions and the panel and is never read aloud by a voice that cannot perform it.

## 1.23.0 — About Me is now Backstory

The settings row that held your Commander's story is now called **Backstory**, in Settings, in the help
pages and in what D47 says it sends to the language model. What you wrote is unchanged, and asking by
voice to set your backstory works as before.

## 1.22.0 — Each core notices its own domain

With Quartermaster aboard, D47 now says what the session is earning: the credits an hour and where
most of it came from, after a sale, a paid mission, a cashed voucher or sold exploration or organic
data. It waits until the session is thirty minutes old, speaks at most once an hour, and from the
second time on also says the rate it gave last time. The core says the figures in its own voice and
changes none of them. Warden and cores you write say nothing new. The new row is Settings ›
Callouts › Plans and stories › **Your core's own subject**. It is on by default, choosing a core
never turns it on or off, and it is silent with personality off.

## 1.21.0 — The Commander's current scenario

Settings › Persona › Core and character has two new rows under About Me. **Current scenario** is the
situation your Commander is in now — a secret cargo run, a refugee convoy, a debt being worked off —
and D47 treats it as true, except that the ship's instruments win where the two disagree. **Who
knows about it** decides who hears it: only those aboard (the ship's AI and crew, the default), those
aboard and your carrier's captain and tower, or everyone, comms included. Both rows are per
Commander and kept between sessions until you clear them. Privacy and egress now says when your
character sheet, About Me and scenario are sent to the language model.

The scenario now also shapes the ship's AI's idle remarks, callout rewordings, and your carrier captain's and tower's lines, to whoever **Who knows about it** reaches. Lines that reword another player's or Frontier's words, such as NPC comms and System Authority patrols, never carry it.

When **Who knows about it** is set to everyone, invented radio traffic and passers-by can be coloured by the scenario too, by what pilots nearby could see you doing. Most exchanges still do not mention it, and nobody asks you about it. With either narrower setting, chatter never sees it.

## 1.20.0 — Invented chatter answers when named

You can now answer an invented NPC who hails you, or a passer-by who remarks on you. Start with their
name — "Vance, thanks" — within 90 seconds of the exchange's last line, and they reply in the voice
the hail used; your next line goes to them too. Each exchange gives four replies at most, and the
fourth is their sign-off. After a jump, docking, undocking or 90 seconds of quiet, naming them gets
"Vance is off the channel." from D47, with no model call. A reply that would contradict your ship or
claim a scan, fine or docking decision is replaced with "Got to go. Vance out." Replies run on the
background model and are billed like any turn. Exchanges you can answer now appear on the In Ship
page, and every invented speaker there, and each reply they give, is labelled "Name (invented)".

Two invented bystanders now react to a kill. When you destroy a ship in normal space and 20 seconds
pass with no further shooting, they exchange two to four lines about it, naming how many ships you
have destroyed there and the last one's ship and faction. A destroyed NPC pilot is named as
destroyed and never speaks; a player's name is never used. It fires for a kill under two minutes
old, at most once per "least time" setting, uses no new setting, and restarts the ordinary gap.
It is not made docked, landed, on foot or in supercruise, and is dropped if you dock, land or enter
supercruise before it is written.

## 1.19.0 — Music and model checks in Settings

Settings › Language model › Provider and model has a Test row below Model. Its text shows whether the
language model is available and why, as "what model are you using" reports it. The Test button asks
the selected provider for its list of models with the stored key and reports that it works, that the
key was refused, or that the service could not be reached, and sets the availability to match. It
runs no completion and adds nothing to the session's spend. The VERIFY button on each language-model
key now uses the same free check; Anthropic keys were previously verified with a paid one-word turn.

## 1.18.0 — Seven more effects join the Guardian voice chain, a rebuy warning, and bookmarks

Invented radio traffic between other pilots and controllers now sounds further off than a hail: the
voice is narrower and sits over more static. It has its own row in the Audio mixer, Overheard
chatter, at level 0.5 with a mute box. Muting it stops those exchanges being written, so no model
call is made for them. Hails addressed to you sound as before.

Spoken answers about your ship, stored modules, engineering and suit now name at most three modules
and give a count for the rest. "My ship" says how many modules are fitted and unpowered and points to
Fleet › Ships for the full loadout; "stored modules" gives the count and the number of systems; "my
engineering" gives the number of engineered modules; "my suit" gives the modification count for the
suit and each weapon. The model is told the same rule when it answers aloud.

Ambient music now plays only while Elite is running. It stops within a few seconds of the game
closing, including after a crash, and starts again for the current situation when the game is back,
unless it was paused or muted. With Elite closed, "resume the music", "next track" and the media keys
reply "Elite is not running." and play nothing, and unmuting Music in the Audio mixer starts no track.

The Ambient music group in Settings now starts with a Now playing row and a Next track row. The first
shows the track name, or Paused, Muted or Nothing playing, with a Pause button that reads Resume while
paused; the second skips to another track. Both buttons are disabled while the music is muted or nothing
is playing, and the row follows changes made by voice or the media keys while Settings is open.

Each invented speaker in NPC chatter is now given a manner of speaking for the exchange, such as
terse, over-explaining, talking in questions or stiffly formal, and two speakers in one exchange never
share one. A manner describes how someone talks, never where they are from: every line is still
written in standard English grammar, with no phonetic spelling or dropped articles for any accent.

A new setting, How often an accent flavours the words, sets how often the model is asked to let an NPC's
words suit the accent of its voice. It applies per line to reworded comms and per speaker to invented
chatter, from 0 to 100 percent in steps of 10, and defaults to 50. The voice keeps its accent either way.

A named character in an in-game message is now spoken in a voice with the accent their name clearly
suggests, when the voice provider has one, and in a voice of the sex it suggests. NPC voices match sex
by asking the language model, in the same request that asks about the accent; without a language model
they ignore sex. The answers are kept in `data\name-accents.json`. The first line from a name that has
not been answered yet is voiced without regard to sex or accent; later systems use the answer. Other
Commanders are cast without regard to sex, invented chatter voices are unchanged, and the language stays
English. The Language model entry under Privacy and egress now says these names are sent to it.

Only an explicit request to deliberate now gets Max effort: "carefully", "think hard", "step by step",
"max effort" and "maximum effort". "Work out", "figure out", "explain why", "walk me through" and "in
detail" get High, and "think about" no longer changes the effort.

When a conversation turn with an Anthropic model calls tools, each later round now sends back the
model's reasoning from the rounds before it. Nothing else sent earlier in the turn changes: memory,
standing directions and the tool list stay as they were, the last round is still shown the tools but
may not call them, and game state that changed while a tool ran is added after the tool results. The
reasoning is not kept once the turn ends.

A model turn that streamed thinking or asked for a tool before it failed is no longer sent again. It
was billed a second time and delayed the answer; ask again instead. A turn that fails before the model
sends anything is still retried. A timeout from d47 no longer marks the model unavailable, so the next
turn reaches it.

A conversation turn now has one output limit for all its rounds: 8,192 tokens at Low, Medium and High
effort, and 16,384 at Xhigh and Max. Each round is given what the earlier rounds left. A turn with
fewer than 1,024 tokens left stops without another request and says it ran out of room.

A turn that runs out of room now says what to do next: ask for one part at a time on a model with no
effort setting or at Max, name "Never think harder than this" when it is holding the turn, and
otherwise ask again with "think carefully".

ElevenLabs v4 Turbo is now the default ElevenLabs model. It performs delivery direction, such as a
sigh or an alarmed reading, as v3 does. v3 and Flash 2.5 are still offered in the ElevenLabs model
row, and a model you chose there is kept. The speaking rate row is hidden while v4 Turbo is
selected. The ElevenLabs list price is now $0.04 per thousand characters, down from $0.05, which
matches ElevenLabs' published price for all three models.

Claude Sonnet 5.5 is now Anthropic's default model, at the same price as Sonnet 5: $2 in and $10
out per million tokens. Live game state reaches it as a system message, as on the Opus models, and
a prompt of 512 tokens or more is cached. Sonnet 5 is still offered in the Model row, and a model
you chose there is kept.

A shield booster that already has Super Capacitors no longer counts as work still to do. The
Engineers page counted it against Mel Brandon, Felicity Farseer, Lei Cheung and Didi Vatermann, and
the Fleet page showed the slot as unfinished, because Elite names the effect "Super Capacitors" and
the plan names it "Super Capacitor". An applied experimental is now matched on the journal's symbol
rather than its display name.

An engineer's Planned work, and What unlocking them buys, no longer lists engineering your ship
already has. A slot whose blueprint is applied but whose experimental is not lists only the
experimental, and an engineer with nothing left to do shows no Planned work at all. The unlock
ranking no longer counts finished work either. A ship d47 has not yet seen a Loadout for still
lists all of its planned work.

Page content on every tab now runs to the window's edge, with no border drawn around it, matching
the design.

A dialog can no longer be minimised. It has no minimise button, and Win+Down and the system menu
do not minimise it either. A minimised dialog had no taskbar button and left the main window
unusable until it was found and restored.

Eight dialogs now open as pages of the panel instead of separate windows: Logbook, Debrief, Your
own cores, Coverage, HOTAS switches, What changed, the Audio recorder and Help improve D47. Each is
named in the breadcrumb and drawn at the panel's width, and Back, Esc, the breadcrumb or its Close
button returns to where it was opened. The Settings buttons that open them now work in the headset
panel too, and open the page there.

The remaining dialogs — First run, Spend, Memory, Lore, Macros and every confirmation — are drawn
inside the main window instead of as separate windows. Each sits centred over a dark scrim that
covers the panel below the title bar, with an orange frame, an uppercase title and its buttons at
the left. Esc or a click on the scrim closes it; a confirmation closed that way is a no. The
dialogs follow the panel's zoom.

Memory, Lore, Macros and the Forget key confirmation now open in the headset panel. Pressing their
Settings buttons in the headset used to show "Not currently supported in VR". Each is drawn
over the same scrim, and the ray presses its buttons; a text box in one opens the headset keyboard.
A press on the scrim or the controller's Back closes it.

Badges and cards are filled, with no outline. The Current ship badge on a Fleet card is a cyan
chip with dark lettering, and the PRE-RELEASE badge is an orange chip. The update banner, the
startup and switch readouts in the status row, the choice card, the headset overlay card and the
subsystem level count lose their outlines. An empty priority on the Power page is a filled block
rather than a dashed box.

Errors and cautions are now drawn as the design's notices: a coloured bar at the left on a tinted
ground, with a label above the message. Something that failed is red; a caution you can act on is
amber. This covers the settings error banner at the top of the panel, a failed reply in the
transcript, messages under settings rows, stored-key problems, and the problem lines on the
Macros, Personas, Switches, Adventures, Checklist, Utilities and Routing pages. A VERIFY that
could not reach the provider is amber rather than grey. The Materials notes about engineer ranks
and missing totals are amber notices with a SHOW tile, rather than red buttons.

When you analyse an organism, D47 now says what it is worth and how much organic data you are
carrying unsold: "Cactoida Cortexum analysed. That run is complete. Worth 18.3 million with the
first footfall bonus. 138.2 million unsold." The first footfall bonus is applied where the body's
scan said nobody had walked there, and where no scan said either way the line says the bonus is
not known. A species D47 has no value for is named and left out of the total. Ask "how much
exobiology data am I carrying" for the total, the number of analyses and how many carry the bonus.
The total drops by each species you sell at Vista Genomics, goes to zero when you die, and is
rebuilt from your journals when D47 starts. "Reset unsold exobiology" sets it to zero; the model
cannot do this for you.

Radicoida Unica is now in D47's exobiology table at 119,037 credits a sample, so analysing it is
valued and counted in the unsold total like any other species.

When you finish mapping a body with the surface scanner, D47 now says about what it is worth and
about how much mapped data you are carrying unsold: "Praea Euq BF-A d95 5 e mapped, efficiently.
About 2,258 Cr. About 3.3 million unsold." Elite never writes a body's value, so D47 estimates it
from the body's scan with the formula players have worked out from sales, and says "about" every
time. The total counts only bodies you mapped, so Universal Cartographics pays more than it for
the stars and the bodies you scanned without mapping. Ask "how much exploration data am I
carrying" for the total and each body. The total drops for every system a sale names, goes to zero
when you die, and is rebuilt from your journals when D47 starts. "Reset unsold exploration" sets it
to zero; the model cannot do this for you. The callout has its own switch, Mapped body value,
under Exploring.

The exobiology route plotter now checks the galaxy search setting before asking Spansh for a
route, the same as the neutron, Road to Riches and trade plotters. With it off, "plot me an
exobiology route" gets the same switched-off answer as the other plotters, rather than sending the
origin system and jump range regardless.

An exobiology route is now kept once it is plotted, the same as a Road to Riches loop. The Routing
tab's Plan page has an **Exobiology** card with stops, radius, the least a body must be worth and
coming back to the start, plus **Show most recent** for the last route. Its page lists each stop's
bodies by value with the species on each and what they pay, marks stops reached as you arrive, and
"plot next exobiology stop" puts the next system in the galaxy map.

Flanger, phaser and wah now run after Chorus; deep ring mod after Ring modulation; tremolo,
overdrive and bitcrusher before Glitch. Each has its own level, the same as every other Guardian
voice effect. Three new presets use them: Ring-mod rasp, 8-bit computer and Flanged vocoder.

A new Stutter effect, first in the chain by default, repeats a word's start two or three times and
occasionally pitch-shifts whole phrases. A new Stutter host preset ticks it with Chorus.

Two new effects keep the voice and change only its pitch: Monotone holds it at the core's base
pitch, and Stepped pitch moves it to the nearest semitone so it jumps rather than glides. Two new
presets use them: Flat robot and Stepped synth.

Four more effects: Hive layers three pitch-shifted copies under the voice, Whisper takes the tone
out of it, Reverse reverb swells a backwards tail into each word, and Shimmer adds a reverb that
rings an octave up. With Reverse reverb ticked, every line starts 0.3 seconds later. Three new
presets use them: Hive chorus, Ghost and Shimmer core.

Three more effects reuse the comms-link sound over-the-air voices already have: Helmet puts the
ship AI's own voice through it at full strength with a click at the start and end, and Hologram
puts it through at whatever strength the level sets, losing stretches of the voice under static
the weaker it gets. Respirator adds a synthesised breath after each sentence. Three new presets
use them: Helmet comms, Respirator and Hologram.

Pressing **Test** under Guardian Voice Effects closed D47 when the voice service returned no audio.
It now says under the row that the test could not be done, and why. The same applies to every other
setting with a button that speaks.

A ship voice the selected speech provider does not offer, such as an OpenAI voice stored while Edge
Neural is selected, is now replaced by one of that provider's voices once its list arrives, for
every core. A voice in the list, including one you chose, is left as it is.

When your balance drops below the rebuy on the ship you are flying, your core now says so once, for
example "Rebuy on this ship is 3.2 million credits. You have 1.1 million." It says it again only
after your balance has covered the rebuy and fallen short again, or when you change ship, and says
nothing while you are in multicrew or a taxi. The **Rebuy cover** switch under Flight callouts turns
it off.

A system search can now name a minor faction: "find systems near me where Eurybia Blue Mafia is
present", or "which systems does Eurybia Blue Mafia control". It combines with distance,
allegiance, government, security and state. A faction your journals have recorded is matched
from part of its name or a different capitalisation, and the answer says what it was read as, for
example "Read as Eurybia Blue Mafia." Any other name is sent as you said it; if nothing matches, the
answer says the name may be misspelled and suggests close names from your journals.

Each system a faction search finds now says who controls it, how much influence the faction you
named has there and when the system was last reported, for example "controlled by Eurybia Blue
Mafia; Eurybia Blue Mafia at 67.4% influence; reported 2026-09-25". Searches that name no faction
are unchanged.

You can now bookmark a destination. With a system, or a station in the system you are in, targeted,
say "bookmark this" and it is saved under the destination's name: "Bookmarked Jameson Memorial, in
Shinrarta Dezhra. Say 'set course for Jameson Memorial' to go there." Saying "set course for" the
name later plots the course. "What are my bookmarks" lists them, "rename bookmark Jameson Memorial
to Home" renames one, and "delete bookmark Home" deletes one. Bookmarks are kept per Commander and
are d47's own, not the galaxy map's.

The Routing tab has a new Bookmarks page listing each bookmark with its system and the date it was
made, with Rename and Delete buttons.

A value refused in a typed prompt, in the adventure editor or when renaming a bookmark, now shows
why it was refused. The reason used to be replaced by the spelling instructions as soon as it
appeared.

The ask box takes more than one line. Shift+Enter starts a new line, Enter still sends, and pasted
line breaks are kept. Up and Down walk what you have sent from the box's first and last line; on
any other line they move the caret instead.

A new Tabs setting, under Interface → Window, puts the panel's tabs down the left-hand side
instead of along the top, on the window and the headset panel alike. Help, the avatar and the
other controls from the tab row move to the right-hand end of the row above the page. Up and Down
move between the tabs in the rail.

Audio dropped into `data/audio/` no longer holds up push-to-talk and callouts while it loads. d47
now waits until nothing in the folder has changed for three seconds, then reloads it in the
background, once, however many files were copied in.

Ambient music is read from disk while it plays instead of being loaded whole. Memory used for
music no longer grows with the number or length of the tracks in `data/audio/music/`, and
reloading the folder no longer reads every track. A track that cannot be read is logged and the
next one starts.

Audio dropped into `data/audio/` can now be `.mp3`, `.m4a`, `.aac`, `.wma`, `.flac` or `.wav`, at
any sample rate and channel count. d47 converts each file to 48 kHz mono when it loads it, so a
stereo 44.1 kHz file no longer shows as "Skipped" on the **Your own audio** row. A file Windows
cannot decode is still skipped, and the row says so; on a Windows N edition, MP3 and AAC need the
Media Feature Pack.

Sound cues, alerts and the thinking bed can each have several files of your own, and d47 picks one
at random each time. Put them in `data/audio/cues/<state>/` (for example `cues/listening/`),
`data/audio/alerts/<alert>/` (for example `alerts/under-fire/`) or `data/audio/beds/`, under any
file name. Each folder plays every file once before any repeats. A folder with nothing in it plays
the sound d47 ships with, and the folders are created empty on first run. Alerts can now be
replaced for the first time. A file directly in `cues/`, such as `cues/listening.wav`, is no
longer read: move it into the folder for its state. The **Thinking bed sound** setting and the
`thinking-pulse` bed are removed.

The **Your own audio** row under Audio mixer has an **Open audio folder** button that opens
`data\audio` in Explorer. With nothing in the folder, the row reads "Nothing in data\audio yet."

A settings group with nothing that can be reset, such as **Your own audio**, no longer shows a
reset button in its heading.

Ambient music now follows the music Elite itself is playing. `data/audio/music/` has a folder for
each of Elite's music tracks, among them `combat-dogfight`, `galaxy-map`, `docking-computer`,
`main-menu` and `interdiction`, all created empty on first run. When the folder for Elite's current
track has files, d47 plays from it; otherwise it plays from `docked`, `supercruise`,
`normal-space`, `on-foot` or `general` as before. Set Elite's music volume to zero (Options, Audio)
to hear your music instead of the game's. The Audio help page lists every folder.

The keyboard's play/pause and next-track keys now control d47's ambient music. Pause holds the
track where it is and a second press resumes it from that point; next track starts another track
from the same folder. d47 appears in the Windows volume flyout with the track's file name and
working buttons. Saying "pause the music", "resume the music" or "next track" does the same. While
paused, a change of situation starts nothing until you resume. Pause lasts until you resume or
restart d47 and does not change the Mute setting. Windows sends the keys to whichever player was
used last, so with Spotify also open they may go there instead.

When an ambient music track ends, two seconds of silence now play before the next one starts; the
two used to run together. A change of situation, unmuting and next track still start music at once.

Asking "how many merc coins do I have" reads out your bank account statistics, including the Merc
Coin balance, without going to the model. Merc Coin figures in your career statistics are given in
Merc Coins; they used to be given in credits, under names such as "Merccoins Total Spent".

Every button is now 44 pixels tall with a 13-pixel label and 16 pixels of padding each side. The
tabs along the top are 44 tall too. Buttons that were drawn smaller are now full size: the Fleet,
Engineers and hull picture buttons, Checklist Edit, Delete and the move arrows, proposal Accept and
Decline, the chooser's commit button, Settings' confirm and press-row buttons, Guardian Save and
Rename, and Send.

Hovering a button, a stepper arrow or a top tab now lightens its background and leaves its label
unchanged, instead of filling it with the theme's highlight colour. The solid fill is kept for a
pressed button, a button reached with Tab, and the selected tab. A destructive button still fills red on hover.

A button that deletes or removes something now has a dark red background even when the pointer is
not over it, so it stands apart from the buttons beside it. The same applies to **Delete key** in the
dialog that asks before a stored API key is deleted.

Buttons, fields and chips that sit next to each other are now 2 pixels apart, instead of 4 to 10:
Send and the message box, proposal Accept and Decline, Update now and Later, Help beside the tabs,
the key-binding row, the API key controls, the on-screen keyboards, the checklist bar and movers,
and the button rows in the Logbook, Debrief, Spend, Switch, Macro, route and loadout pages. Space
between buttons and text is unchanged.

Every checkbox now has its box before its label; some had it after. The Engineers filters, the
checklist's **Goals** and **Include Partial Grades** toggles are written in capitals like the other
buttons beside them, and the checklist toggles are the same height as those buttons. Capitalised
checkbox labels are 13 pixels rather than 14. A checkbox that cannot be changed now has a grey
background.

Segmented choices — the rows of side-by-side options in Settings and on the Macro, Spend, Switch,
Logbook, Adventures, Fleet and Power pages — are now 44 pixels tall with 13-pixel capital labels.
An option with a status line under its name is 56 tall, and the status line is now drawn: small
capitals, grey, yellow when a key is stored, and brown on the selected option. Previously the
status line was replaced by the text "AVALONIA.CONTROLS.STACKPANEL".

A choice stepped through with ◄ and ► arrows now shows its value left-aligned in 14-pixel Saira
rather than centred in 16-pixel Sintony, with its position ("3 / 6") smaller and dimmer. A number
between arrows, such as **Capture before the key**, is 14 pixels rather than 16. Level bars have
taller segments, 22 pixels rather than 16, and a 13-pixel value beside them rather than 16.

Buttons that show only a symbol, such as ↺ to reset a setting or the ↑ ↓ ✕ step buttons on the
Macro page, are 32 pixels square rather than 34, and ↺ is 16 pixels rather than 15. The name that
appears on hover now opens to the left of the button rather than the right, 32 pixels tall, in
lighter letters spaced slightly apart.

The COPY buttons are now square buttons showing two overlapping squares: beside system names on the
Fleet, Carrier, Engineers and Routing pages, in the journal file and in the conversation, and beside
the search box for copying the whole page. Pointing at one shows COPY, and COPIED or COPY FAILED for
two seconds after it is pressed.

On the Routing pages, pressing **Plot**, **Find it** or **Copy** with a required field empty now
outlines that field in red and writes what is missing under it, beside a short red bar, rather than
in a red line further down the page. The message goes as soon as you type in the field. The Routing
fields are 44 pixels tall rather than 30. The search field above a page is outlined in a dim line
that turns orange while you type in it, with 14-pixel text rather than 16.

Numbers, costs, times and dates are now in the monospaced typeface: the Utilities clocks and their
dates, the Carrier page's figures and tritium, the Fleet page's ship figures, an engineer's
distance, the community goal totals on the Routing pages, the times in an adventure's story, the
price on the Logbook's **Write it** button, and the times and dates in the Audio recorder, Logbook,
Coverage, Debrief and Memory windows. Names, words and system names stay as they were.

List rows are 50 pixels tall rather than 44. A row's name is in capitals in the heading typeface,
the line under it smaller capitals in orange, and a figure at its right in capitals too. This
covers the Engineers, Bookmarks, Utilities, Help, Adventures and on-foot material lists, the
choosers, and the Coverage, Logbook, Lore, Memory and Debrief windows. Sentences in a row, such as
a checklist line, a help description or a learned phrase, keep their own case and typeface. The
group headings in the Fleet slot list, the Engineers list and the Checklist's **Done** section are
smaller orange capitals over a dim line.

The title row at the top of the main window and each dialog is redrawn: the name is white, 19
pixels rather than 23 and spaced wider apart, and the version beside it is in the monospaced
typeface, 12 pixels, in grey. The minimise, maximise and close buttons are grey at rest; pointing
at minimise or maximise now gives a dark tile with an orange symbol rather than a solid orange
square. Close still turns red. The symbols' lines end square rather than rounded.

Screen titles are in capitals, spaced slightly apart, over an orange line 10 pixels below them:
Settings, Fleet, Carrier, Engineers, Checklist, Help and the Routing, Adventures, Bookmarks and
Utilities pages. A route's summary at the top of a Routing page keeps its own case. On Settings the
area name above the title is in the same weight as the title's. The Carrier page shows the
carrier's balance at the right of its title rather than among the figures below. A group heading's
orange line sits 6 pixels below it rather than 4.

Glow now shows only on the brand diamond and name in the title bar, the push-to-talk dot and the
active tab, in the themes that glow at all. The Bloom setting is gone, and every glow is drawn at
the fixed strength it used to default to.

The figures in Help and the docs site now have square corners and flat line caps, matching every
other panel.

Saying a command without its "the" now works the same as saying it with one, and the other way
round: "open galaxy map" reaches the galaxy map, "target next system" targets the next system, and
so on for every declared phrase. "Engage hyperspace" and "hyperspace" now also engage the frame
shift drive.

The **Your own audio** row now accounts for every file under `data\audio`, not only the ones it
picked up: a count for each folder with files in it, then every file that was never a candidate —
loose in the wrong place, in a folder D47 does not read, or not a format D47 reads — and finally
every file that was tried and would not load.

On the Journal File reading, choosing a line while a search is typed now shows that line's fields.
It showed the fields of whichever event sat at the same position in the unfiltered list.

Invented chatter no longer has anyone scan, interdict, target, fine or put a bounty on you, grant or
deny you docking, give you or take your cargo, or send you a wing or friend invite. Each of those
would have written a journal event, and none did. An exchange with such a line is not played at
all. In controller chatter, where the controller talks to its own pilot, a line counts only when it
names the Commander.

Invented chatter now waits while you are in a fight. An exchange that falls due while Elite shows you
in danger, or within a minute of you being attacked or making a kill, plays once the fight has been
quiet for a minute.

A pilot killed in this system no longer speaks in invented chatter. The next pilot already heard in
the system takes their place.

A reply that reaches the model's output limit now ends with "I ran out of room before I could finish
that answer." Before, it stopped mid-sentence, or said nothing when the model had spent the whole
limit thinking. The unfinished reply is not kept in the conversation, and the log now records why
each model turn stopped.

A long model turn is no longer cut off while the model is still thinking. "Give up after" now counts
seconds with nothing from the model, and every piece of the reply, thinking included, restarts it.
Before, it limited the whole attempt, so a Max-effort turn could fail at 45 seconds and be billed
three times. A timed-out turn now says "It sent nothing for 45 seconds.", and the log records the
longest wait between pieces of each model turn.

## 1.17.0 — The ship remarks on promotions and notable kills

On the Gap page, the two red summary lines now say what they count in plain terms: "N planned
grades need a higher engineer rank" and "N planned slots have no material total". Each opened
list names the ship or on-foot build a line is for, so the same blueprint planned on two ships now
counts and lists twice instead of once. A weapon modification with a different recipe per
manufacturer now counts under "no material total" rather than under the rank line, since it is not
a rank problem.

The Materials page's engineer-rank gate now names who to go and see. Pressing it answers four
questions in order: who covers something you have planned and where you stand with them, which one
to go to first and why, that engineer's route in flying order with the "Add to checklist" control,
and what is still blocked — one line per job, such as "Grade 5 Long Range Weapon · Multi-cannon
×10", rather than one line per slot. The button's own count is in jobs too, and the blocked set
matches what the Engineers Route ranks.

The Engineers Route, and the voice answer about which engineer to unlock next, now put engineers
you only have to fly to ahead of ones whose hand-over is still to gather, and those ahead of ones
whose invitation requirement is not met. Within that, they rank by planned work freed per jump,
counted in jobs: grade 5 Long Range on twenty multi-cannons is one job, not twenty. Work that two
engineers can both roll is credited to the higher-ranked one only. Each candidate now says "N
planned jobs covered", and its working lists one `covers:` line per job, with a count such as ×20
where the job spans several slots.

When you are promoted, the ship's AI now makes one remark about it in its own voice. This covers
Combat, Trade, Exploration, Mercenary, Exobiology and CQC, the Empire and Federation navies, and
Powerplay rank. It keeps the career and the rank name as Elite gives them, for example "Trade,
Elite IV". With personality off, or no model, it says the plain line: "Promoted. Trade, Elite IV."
Joining a Power is not announced, and promotions already in the journal when D47 starts are not
announced either.

When you destroy a ship, the ship's AI now remarks on it if the kill is notable: the first of the
session, the best reward so far, a second kill within thirty seconds, or every fifth kill. It names
the ship, the pilot and the reward, for example "First kill of the session. Paul Curnow's Eagle
destroyed, 59,330 credits." If the pilot taunted you in the five minutes before, the reworded remark
may answer it. The **Notable kills** switch under Flight callouts turns it off.

On a ship's page, POWER now shows two verdicts, DEPLOYED and RETRACTED: whether the build fits
the power plant, or how many megawatts it is over. Pressing them opens a Power page. The page stacks
every module by priority, P1 to P5, with lines for full output and for a damaged plant, and opens
one priority beside the stack so you can see which module each line falls in. Select a priority in
the stack or with the stepper. Switch between deployed and retracted hardpoints. Drag a module to
try a different order within its priority; RESET ORDER puts it back. The order you try does not
change what stays powered, because the game powers a whole priority or none of it.

Under the stack, D47 CHECK lists the modules that are probably in the wrong priority, judged with
hardpoints deployed: life support that a damaged plant would turn off, core modules and modules
needed in a fight that are unpowered, and modules that depend on the build. Where there is a fix,
MOVE TO Pn moves the module to that priority and the page recalculates. A move can push a priority
over the plant's output, and the page shows it. Moves are saved with the ship's build, and UNDO
MOVES puts every moved module back. The moves are D47's own record: they do not change priorities
in the game.

To move any module to another priority, drag its bar out of the opened priority and drop it on a
priority in the stack. The priority under the pointer is outlined while you drag. The module goes to
the top of that priority, and the move is saved with the build like MOVE TO Pn. Priorities in the
stack now have a clear gap between them, and every priority is labelled; a label that does not fit
inside its block sits beside the stack. An empty priority shows as a dashed slot, so it can take a
drop too.

A material's detail on the Materials page now says what kind it is under its name, for example "Raw
material", and opens with **How to obtain**, the fastest method first. Raw materials name the
crystalline shard or brain tree site for their trade group, with its coordinates, then the trade
down from it. Manufactured materials say which High Grade Emission to search for (allegiance, state
and population), then the trade down, then Dav's Hope. Encoded materials name Jameson's crash site
and the trade from there. **Capacity** lists the most that can be stored, how many you hold and how
many are needed. **Needed for** lists each module and blueprint grade with its count, for example
"Thrusters · Dirty Drive Tuning 5 — 12", without naming ships. The trade that would cover a
shortfall is under **Potential trades**.

The Materials page now costs a planned blueprint when D47 does not know which module is in the slot.
Some blueprint names belong to more than one module, such as Heavy Duty, which is on both Armour and
Shield Boosters. D47 now works out the module from the module the plan names, then the blueprint,
then the module fitted in that ship's slot, then what the slot can hold, so Heavy Duty on a utility
mount is costed as a Shield Booster without the ship's loadout. Plans that name a bulkhead, such as
"Anaconda Lightweight Alloy", are costed as Armour. Where more than one module is still possible, the
slot is costed at the most expensive of them, and a note says so. **Can't be costed** now lists only
blueprints or grades D47 has no recipe for, and plans with no grade.

Say "night vision", "night vision on" or "night vision off" to toggle night vision in the ship, in
the SRV and on foot. In the ship and the SRV, "night vision on" presses nothing if night vision is
already on. On foot, "on" and "off" both toggle it, because D47 cannot yet tell whether it is on.

D47 no longer forgets your parked ships' modules when it starts. A save made before D47 had finished
reading older journals replaced every remembered ship with the one you were flying. A ship is now
removed only when the journal says it was sold or replaced. When a ship you have a build for is not
in the recent journals, D47 now looks further back for its last loadout at startup, so you no longer
need to rescan to recover it.

Each Guardian voice effect now has a level from 1 to 20, and the ticked effects run in an order that
is saved with them. At the default levels and in the default order the effects sound exactly as
before, and a settings file from an earlier version keeps the effects you had ticked. The Guardian
voice help names the parameter each level sets.

Guardian Voice Effects — renamed from Guardian voice — now has a Preset row at the top, and every
row in the group is shown rather than folded. Four built-ins are offered: Off, Vocoder, Deep core
and Damaged core, each ticking its own effects at the default order and levels. You can also save
your own combination of ticks, order and levels under a name, load it back, update it, rename it or
delete it; the row reads Custom the moment anything about the current combination stops matching a
preset.

Guardian Voice Effects on the Its voice page is now one block. The Preset list opens inside the page
and pushes the effects down, with your own presets in cyan under "Your presets", and Custom reads
"Custom · changed from <name>" after you change one of them. Under it every effect is one line in
the order it runs: its number, its box, a 20-segment level you can click or drag, and − and +
buttons showing the value in its own units. Test reads PLAYING while the sample plays, and the
group's reset turns every effect off and sets the order and levels back to their defaults.

The Guardian effects can now be put in a different order. Drag an effect by the dotted handle at
the left of its line: the other lines move aside and renumber as you drag, and the new order is saved
when you let go. With the handle focused, Up and Down move the effect one place.

The Guardian Voice Effects preset row now has SAVE AS and RENAME buttons after TEST. SAVE AS is
disabled on a built-in preset and opens a name row on Custom; RENAME opens it on one of your own
presets. Enter confirms, Esc or CANCEL closes it without writing, and a rejected name shows the
reason in red. A save or rename lands straight away and a notice under the preset row says "Saved
<name>." or "Renamed <old> to <new>.", clearing itself after six seconds.

The preset row now also offers UPDATE and DELETE. UPDATE appears once Custom reads "changed from
<name>" and writes the current effects into that preset straight away. DELETE appears on one of
your own presets, unchanged, and removes it with one press — there is no confirmation step. Both
notices carry an UNDO button that puts the preset, the effects and the basis back exactly as they
were, for as long as the notice is showing.

## 1.16.0 — On-foot modifications say what they do

Each suit and weapon modification now has one sentence saying what it does. The modification
picker shows it under each name, a slot's Planned block shows it under the planned modification,
and asking about a modification, for example "what does stowed reloading do?", includes it in the
answer. The sentences come from Odyssey Materials Helper. They say what a modification does and
give no figures.

Reopening the picker on a slot that already has a plan marks that modification "planned now", as
the ship blueprint picker does. The highlighted row's description is now dark on the orange
highlight, where it was grey and hard to read. On a slot's page the sentence is shown in the same
colour as a planned ship blueprint's effect.

Asking about a modification that exists in several versions, such as "Higher Accuracy", now
gets a question back, "Kinetic, Plasma, or Thermal weapons?", instead of "I have no on-foot
modification called ...".

## 1.15.0 — The model knows your Powerplay standing

Asked "who am I pledged to in Powerplay?", D47 said it did not know. It now tells the model which
Power you are pledged to and your rank with it, from the Powerplay event Elite writes at login. After
you leave a Power it answers that you are not pledged. After you join or defect, the rank is given
as not yet known until Elite next reports it.

It also tells the model your merit total with that Power, from the same login event, and keeps it
current each time Elite reports merits earned. Joining, defecting or leaving clears the total until
Elite next reports it.

## 1.14.0 — The carrier balance counts its upkeep

Elite takes your fleet carrier's upkeep at the Thursday weekly tick and writes nothing to the
journal when it does. D47 now works out the weekly upkeep from the balances the journal has
recorded, and takes it off the last recorded balance for each tick since. The Carrier page shows
the balance as "about" that figure, with Upkeep and Covers tiles and a line giving the recorded
balance and its date. "How is my carrier" says the same. Until two recorded balances with a tick
between them are available, the page and the report show the recorded balance only. Income from
sell orders that other Commanders fill is not in the journal, so it is not counted. The squadron
carrier is not adjusted.

## 1.13.13 — API keys stay hidden until you replace them

In Settings, a stored API key now shows as eight bullets with KEY STORED, REPLACE and a red FORGET
KEY, and VERIFY where the provider can check a key. There is no input box until you press REPLACE,
which opens the box with SAVE and CANCEL. CANCEL leaves the stored key as it was. With no key
stored, the box and SAVE show straight away. FORGET KEY asks before it deletes the key, as the undo
arrow inside the box did before; that arrow is gone. VERIFY beside a stored key checks that key
without anything typed. This applies to every key row: Voice input, Its voice and Language model.

## 1.13.12 — The levels are one mixer

On Settings > Sounds and levels, the Levels group is now one table with a row for each channel:
Thinking bed, Ambient music, Sound cues, Speech and Alerts. Each row has the channel's level bar, a
Mute checkbox and, for the three channels that duck, a "Duck while D47 speaks" bar. Speech and Alerts
show a dash there. The page used to repeat Level, Mute and Duck on thirteen rows without saying which
channel each row set. A row's ↺ resets that channel's level, mute and duck together. Hovering a
channel's name says what the channel is. Filtering by a channel's name, such as "music", leaves that
channel's row.

## 1.13.11 — Read-only values on grey

In Settings, every read-only value now sits on a dark grey block in white text, not in orange beside
a thin bar. Under Its voice > What it costs, each figure has its own block: "Spoken this session"
shows the characters and the cost, and "Spoken by each voice" shows one block per voice, such as
"115 via ElevenLabs · $0.0058". On Privacy and egress, the full detail of each entry opens from a
SHOW button that reads HIDE while it is open. FORGET THEM ALL under Voice input > Corrections and
FORGET AND PAIR AGAIN under Its voice are red. The second was labelled "Pair every voice again". Both
do what they did before.

## 1.13.10 — Step a suit's grade on its page

On Fleet > Suits, a planned grade on a suit or weapon now has ▲ and ▼ buttons beside it, as a
planned ship slot's grade does. Pressing one changes the plan to the next grade between 2 and 5, and
what it costs follows. The plan line reads "At Pioneer Supplies", with the grade beside it. The first
grade is still chosen from the row of buttons. Under "What it costs", a line such as "Credits from
grade 3: 12,000,000" gives the credits for the upgrade from the item's current grade, the same
figure d47 says when asked. It is left out when d47 does not know the item's price.

## 1.13.9 — Your suit is not a ship

On Fleet > Suits, the suit you are wearing now carries a CURRENT SUIT badge and each weapon you are
carrying carries CURRENT LOADOUT. Both said CURRENT SHIP before. The "on you" line under them is
gone, since the badge says the same thing. Ship cards still say CURRENT SHIP.

## 1.13.8 — Your carrier after a restart

Fleet > Carrier shows your fleet carrier again when d47 starts while Elite is already running. It
read "No carrier has turned up in the journal yet" because the carrier's location, written at login
without a callsign, stopped d47 taking the callsign, name and stats from older journals. It now takes
them and keeps the location from this session.

## 1.13.7 — Grades picked, entries heard first

Every prompt that asks for a value now opens listening, with the text box focused and the Keyboard
checkbox unticked. Timer minutes, alarm time, editing a checklist line, and an adventure's name,
chapter title and rank used to open with the drawn keyboard up. The keyboard appears when Keyboard
is ticked, or when d47 did not catch what was said. Its keys were too small for their padding and
showed only fragments of each character; every key now shows its whole label.

A grade is now chosen from a row of buttons, and one press sets it. For a suit or weapon the row
is 2, 3, 4 and 5, with the grade already planned outlined. For a ship slot on a hull d47 has no
layout for, the row is 1 to 5 and Any. There is no text box or keyboard on either. Saying "four",
"grade four" or "4" while the row is showing sets grade 4. A grade outside the row sets nothing, and
d47 says which grades it takes.

## 1.13.6 — Engineering Frontier added in September

The Ships tab now lists the blueprints Frontier added this month among what a module can be
engineered with, including Balanced and Support Focused on a power distributor and Long Range on a
Detailed Surface Scanner. d47 has no recipe for them yet, so each one says Frontier engineers it and
d47 has no recipe for it, instead of leaving it out. A Detailed Surface Scanner's Expanded Radius
blueprint is still offered with its ingredients.

## 1.13.5 — Engineers judged the way Elite judges them

A referral into an on-foot engineer is now met once the referrer is unlocked. On-foot engineers have
no rank, and d47 was asking for grade 3 with them: Terra Velasquez's first step read "Grade 3 with
Jude Navarro." and was never met, the route added a stop to rank Jude up, and the checklist item
"Rank 3 with Jude Navarro" was never done. The step now reads "Unlock Jude Navarro." and is met when
Jude is unlocked; Yi Shen's is met when any one of Baltanos, Eleanor Bresa or Rosa Dayette is. On the
Engineers tab, an on-foot referrer's note reads "unlocking them opens …" rather than "grade 3 opens
…". Asked how to unlock an on-foot engineer, d47 no longer mentions a grade or a count of
modifications, and says whether the referrer is unlocked. Ship engineers are judged on rank as
before.

Suit and weapon modifications now name the Colonia engineers who offer them: Baltanos, Eleanor
Bresa, Rosa Dayette and Yi Shen. A suit plan with Night Vision lists Yi Shen beside Oden Geiger in
its Engineers block, and the route counts them too. A Night Vision checklist item no longer names
Oden Geiger as the only engineer.

A ship slot plan no longer names an engineer. Any engineer who can roll the planned blueprint at the
planned grade can do the work. `plan_ship_build` has no `engineer` parameter, a plan line no longer
ends ", with Felicity Farseer", and putting a build on the checklist no longer adds a "Rank 5 with
…" item. The cost is counted at the best-ranked unlocked engineer who can roll it, and the
Engineers route credits the slot to every engineer who can. A build saved with an engineer loads as
before and drops it when next saved; items already on the checklist are left as they are. When a
changed build is put back on the checklist, items already there keep the order you put them in.

## 1.13.4 — Settings controls fit their values

The Hearing provider and Voice provider rows, and each voice's provider row, now show just the
provider's name. The line under it says where the provider runs and whether its key is stored: THIS
COMPUTER · FREE for Whisper and Kokoro, FREE for Edge Neural, and PAID · KEY STORED in yellow or
PAID · NEEDS KEY for a paid provider. Storing a key changes the line straight away. A stepper's
position, such as 1 / 5, is now inside its value box, and a stepper is at most 420 wide. A row of
four options that does not fit on one line is two rows of two equal tiles, with long labels wrapped
inside them.

A choice with more than seven options, such as Persona or a Microphone list on a machine with many
input devices, is now a filled tile showing the current value with ▼ at the right, the same as the
Voice and Output device rows. Pressing it opens the list as a page of the panel, with a filter and
"Use the default" where the row has one, on the desktop and in the headset. The Local voice model
build row keeps its arrows, because it asks before downloading.

A number row, such as Capture before the key, is now a 220-wide control: ◄, the value with its unit
in capitals (500 MS, $0.05), then ►. The arrows move by the row's step and stop at its limits, and
repeat while held. Click the value to type one; Enter or clicking away applies it, and Esc cancels.
The two speech price rows now show a $ unit. A level, opacity or duck row is a bar of 20 segments
with the value beside it, such as 0.85: click a segment to set it, or use the Left and Right arrow
keys. Segments below the row's minimum are dim and set the minimum. A binding row shows the bound
key in capitals, or NONE, then BIND to capture a new one and CLEAR, which is greyed out when nothing
is bound. Without a controller, a stick-button row reads NO CONTROLLERS and BIND is greyed out.

On/off settings in a settings group are now checkbox tiles in a grid, two across, where the group's
first on/off setting was. Click anywhere on a tile to switch it. Guardian voice shows its eight
treatments as two rows of four, Cancel D47's own voice and Take the room out are side by side at the
end of Microphone, and Loop-state cues and Thinking bed are side by side under Cues. Tiles have no
reset of their own; the group's reset puts them back. Capture before the key now comes before the
two Microphone tiles. The Mute rows under Levels are unchanged.

## 1.13.3 — Spend and the transcript

The Transcript's footer is rearranged. SPEND has moved from the turn line to the right end of the
microphone row, with SESSION and the session's spend beside it. The figure counts the model's
answers and paid speech, such as ElevenLabs, together, and hovering over it shows the breakdown. The
Spend window's header and Session row show the same total. It updates after each answer, after each
spoken line and after you reset it in the Spend window. Speech with no rate set adds nothing. PTT READY
is now cyan. The microphone row and the ask box now start at the same left edge as the turns.

Each turn's time now follows its tags after a short gap instead of sitting at the far right. Your
own turns are headed with your commander name, such as CMDR JOHN DEPARAGON, or CMDR when d47 does
not know it yet. The search field is wider, and its border is dim until you click into it.

Each answer now shows its own cost line under its text, such as ANSWERED VIA CLAUDE-SONNET-5 ·
EFFORT MEDIUM · $0.0690, and earlier answers keep theirs. An answer that needed no model names the
route instead, with no cost. The line under the transcript is gone. What a turn is doing while it
runs, such as retrying, and the switch and startup notices now show in the microphone row, left of
SESSION.

## 1.13.2 — The honk takes

The arrival honk now works when you arrive in combat mode. In supercruise, where your hardpoints are
stowed, d47 switches the HUD to analysis mode, holds fire for the scan, and switches back to combat
mode. If the HUD does not change within two seconds, d47 says "I could not switch to analysis mode
to honk" and holds nothing. In normal space it still refuses, because there holding fire could fire
a weapon. When the HUD mode has no key d47 can press, the refusal now says that.

d47 now checks that the arrival honk scanned the system. If Elite does not record a discovery scan
within three seconds of the hold ending, and you are still flying in analysis mode, d47 holds fire
once more. If that also produces no scan, it says "The honk did not take". A honk you make by hand
while d47 is waiting counts, and nothing is repeated. When d47 switched to analysis mode for the
honk, it now switches back after the scan, or after the second attempt.

"What time is it", "what's the date" and "what day is it" now work in every run, not only with
`--utilities`. d47 answers them itself with the date in the game and on your own clock, and the
ship's AI is given both dates with every question. Timers and alarms still need the switch. The
help pages are now split into Clock and Timers and alarms.

On a curved headset panel, the ring cursor and presses now land where the panel is drawn, out to its
left and right edges. Before, d47 treated the panel as curving away from you rather than toward you,
so the cursor sat partly behind the panel and buttons near either edge could not be pressed.

## 1.13.1 — The checklist stays quiet at login

Starting d47, or loading into the game, no longer reads out finished checklist items as newly done.
In the few seconds before Elite reports the ship's modules, d47 saw an empty ship and marked every
engineering item on it as not done, then announced each one as finished when the modules arrived.
It now waits for the modules, and a module that really has changed is still reported.

A checklist item is now announced as done only when you have just finished it: an engineer's roll
on that module, or a grade upgrade on that suit or weapon. Everything else ticks the item on the
Checklist page without a word, including engineering finished in an earlier session, a module fitted
in outfitting, and an engineer unlocked or ranked up.

Asking what you are flying now lists every module fitted, slot by slot under the outfitting screen's
headings, with the blueprint, grade and experimental effect of each engineered one. It used to give
only a count, so d47 could not say which weapons were on the ship. The count no longer includes paint
jobs, decals, name plates and other cosmetics, and unpowered modules are named as the game names them
rather than by their internal symbol. A ship you name that you are not flying gets the same list.

## 1.13.0 — Settings pages drawn on one grid

Each place in the Settings sidebar is now a page of its own. Clicking Voice Input shows Voice Input
alone, under the area's name and the place's title, instead of scrolling a column of cards. The
cards, their collapse arrows, EXPAND ALL, COLLAPSE ALL and the HELP on each card are gone. HELP at
the top of the window opens the guide for the page that is open.

**Show every setting** is a checkbox beside the filter field. The field reads "Filter settings". While
something is typed in it, the open page shows only its matching rows. The sidebar shows the number
of matches beside every place that has any, and draws the rest in grey. They can still be clicked,
and each opens filtered by the same words. A page with no match says so and says how many other
pages have one.

In a narrow window the picker that replaces the sidebar lists pages by area and name, for example
"Voice and hearing › Its voice".

Every group of settings now has a title, and a sentence beside it saying what its rows are for. The
reset arrow is at the right of each group's heading and puts back only that group's changed rows. It
is greyed out while nothing in the group has been changed. The reset for a whole page is gone. A group
with no rows showing is hidden, heading and all, so Wake word no longer shows an empty heading while
you use push to talk. Under Headset, each panel's placement is its own group again, and its reset
arrow also clears where that panel was put.

Resetting Push-to-talk puts back Right Shift. Before, it left push-to-talk with no key.

Every row on a page now has the same columns: the label, then the control, then the reset arrow. The
controls start at one line down the page, and so do the reset arrows, whatever the length of the
label beside them. A long label wraps under itself. A protected row's orange bar no longer pushes its
label to the right of the others. The rows stop at 900 pixels wide, so on a wide window a reset arrow
stays near the control it resets. In a narrow window a wide text box shrinks to fit its column
instead of running past it.

## 1.12.0 — Hearing through a cloud provider

A new **Hearing provider** row under Voice Input › Speech recognition chooses who turns your speech
into words: this computer, as before, or Groq, OpenAI, Deepgram or ElevenLabs Scribe. A hosted
provider needs no speech model in memory, so the local model is unloaded while one is selected. Groq
uses `whisper-large-v3-turbo` and a key of its own. OpenAI uses `gpt-4o-mini-transcribe` and the same key
the OpenAI language model and voice already use. Deepgram uses `nova-3` and a key of its own, and
reports how sure it is of each transcript, so a panel prompt asks you to say it again or type it
when Deepgram is unsure. ElevenLabs Scribe uses `scribe_v2` and the same key the ElevenLabs voice
already uses.

A hosted provider receives the audio of every utterance d47 transcribes, your API key, and the
names from your journal used to recognise proper nouns. Hands free, that is every stretch judged to
be speech, whether or not it was addressed to d47. Privacy and egress shows this under **Speech
recognition**.

When the service cannot be reached, refuses the key or limits requests, d47 says so and does
nothing else. It does not fall back to a local model. With no key stored, a press says the key is
missing and sends nothing. The check that refuses words invented from silence still runs, on
`ggml-tiny.en.bin` from the models folder, when that file is there.

"Can you hear me" names the hosted provider and whether its key is stored.

## 1.11.0 — NPC lines written for their voice and humor

The "A little humor" toggle is replaced by a level and a frequency for three groups: the ship's AI,
NPCs, and your carrier's captain and tower. The rows are under Persona › Humor. The level runs from
0 (none) to 10 (as funny as a stand-up comedian). The frequency is the share of that group's lines
that get humor, and d47 decides line by line. Up to level 6 the humor stays dry, with no puns,
comparisons or whimsy. From 7 those are allowed. When a voice performs delivery notes, a funny line
can also laugh or chuckle. Warnings never get humor.

If you had humor switched on, the ship's AI starts at level 3 and 25%. NPCs and the carrier crew
start at 0. "Humor on" now sets the ship's AI to level 3, and "humor off" sets it to 0.

NPC lines are now written for the voice that speaks them. d47 chooses the voice first and tells the
model its accent, so a re-voiced comms line, a line from your carrier's captain or tower, and
invented chatter use the word choice and idiom of that accent. The model is told not to spell the
accent out phonetically or play it as a stereotype. In chatter, d47 casts the voices before the
exchange is written and the model names the speakers. An NPC already heard in the system keeps
their voice if they speak again. Voices with no accent listed, and the ship's AI, are unchanged.

Empire and Federation ranks in goals and adventures are no longer named one rank too high. An
Outsider was called a Serf, a Recruit a Cadet, and a King or Admiral was called "rank 14".

When a local model's context is too small for the request, d47 now says so instead of reading out
the server's JSON error. It gives the model's context size, the size the request needed, and asks
you to raise the context length in the server's settings. LM Studio loads models with 8,192 tokens
by default, which is too small. A failed turn also no longer says "after 3 tries" when it made only
one attempt.

Local models, such as those run in LM Studio or Ollama, now answer the question you asked. d47
sent your ship's current state as a separate message after your question or after a tool's answer,
and a small model replied to that message instead, for example with "Understood" rather than your
fuel level. The state is now added to the end of your question, or to the end of the tool's answer.

A local model with a small context now answers instead of failing on every turn. When the server
refuses a request as too large, d47 learns the model's context size and asks again, offering the
model 17 tools rather than every tool. If that is still too large, it asks once more without the
earlier turns of the conversation. For the rest of the session every turn to that model starts with
the short list. With "OpenAI-compatible endpoint" selected, the Model row under Language model then
gives the context size, how many tools the model is offered, and the context length to set in your
server to get all of them. Claude, when it searches its tools, now has those 17 loaded without a
search.

d47 now answers "what's my reputation with the Empire". It reads out your reputation with each
superpower as a band and a number, such as "Cordial, 28 of 100", with your Imperial and Federal
navy rank beside the Empire and the Federation. Ask about a minor faction by name, such as "what's
my reputation with Mother Gaia", for its band, number and the date d47 last read it. "What's my
navy rank" and "what's my imperial rank" work too, and none of these needs a language model.

On the Transcript, the COPY button no longer sits on top of the "Search this page" field when d47
starts. The field used to run past the right edge of the panel until the window was resized.

Claude Opus 5.5 is now in the Model list under Language model, and Claude Opus 4.8 is no longer
offered. A model typed into the row is still sent, but d47 no longer prices Opus 4.8 or gives it
tool search. Claude Sonnet 5 is now priced at $2 per million input tokens and $10 per million
output tokens, down from $3 and $15, and gpt-5.6-sol at $4 and $20, down from $5 and $30, so the
spend figures for those models are lower from now on.

## 1.10.0 — Every voice paired afresh

The Advanced speech row "Reset every voice to its pairing" is replaced by "Forget every voice and
pair again". It forgets the voice of every core, the carrier captain and the tower on every provider
you have used, hand-picked ones included, and pairs the provider in use again from its current
list. Any other provider is paired the next time you select it. The row then says what was paired
and on how many providers.

With a language model configured, the model chooses from every English voice on the list, not only
the first 120, and reads ElevenLabs' and Cartesia's own voice descriptions. Without one, d47 picks
a free voice whose labels fit, or one at random where the list has no labels, so every core gets a
voice either way.

The carrier captain and tower now get voices of their own whenever they have none, from the
carrier's provider, instead of borrowing the ship AI's.

Gender now binds only Cora and Analyst Prime. Cartesia's "feminine" and "masculine" labels are read.

No voice is chosen in advance any more. Edge Neural, OpenAI and Kokoro refuse to speak with no voice
chosen, as ElevenLabs and Cartesia already did. Warden is no longer fixed to George on ElevenLabs.

Choosing a voice, a model or any other value from a Settings list now opens a page in the panel
instead of a separate window. The breadcrumb above it leads back to Settings, and Back, Esc and the
mouse's back button leave without changing the setting. The page opens in the headset too, where
these rows used to say "Not currently supported in VR". At the smallest panel size it shows
at least eight voices and the whole gender filter. The voice pages say what playing a voice costs;
every other page shows the row's help.

## 1.7.0 — Elite's colours

In the headset, the dimming behind a chooser is the same shade as on the desktop.

Segmented settings rows (Theme among them) no longer clip to a fixed height: the row now grows to
fit its labels, wrapped lines included. The Theme row's fourth choice is renamed from "Elite
colour scheme" to "My HUD colours".

Every theme now draws from a fixed table of colours, one meaning each, instead of mixing every
shade from the accent. Body text is off-white on Elite instead of amber. Neutral greys stay the
same whatever your HUD colour is, and only the coloured tokens follow the HUD matrix under My HUD
colours. Dark no longer glows or draws scanlines, and the Bloom setting is shown disabled on Dark
as well as Light. The scanlines are slightly lighter. The Control Kit's Ramp section shows every
colour with its hex value.

List rows, segmented choices and steppers are restyled to match Elite. A list row is a tile with a white
name and a coloured second line. The selected row fills solid in the accent colour with dark text
in place of the bar at its left edge. Segmented choices are equal-width tiles 2px apart in capitals,
with no frame around the group, and the chosen one fills solid. When they wrap, each line holds
the same number of tiles, give or take one. A stepper's arrows are tiles that
fill solid on hover, either side of the value, and its position reads in grey. None of these glow
any more. The Control Kit has a Choosing among items section showing every state of each.
Settings cards sit on the page background, so a segment's options show as tiles.

Buttons and tabs are flat tiles with no outline, clipped corner or slant. A button reads in the
accent colour in capitals and fills solid when you hover over it, press it or reach it with Tab. A
delete button does the same in red. The primary button no longer stands out from the rest, and no
button glows. Tabs fill solid on hover and when selected, the strip sits on an accent-coloured
rule, and only the selected tab glows. The second row of navigation reads in the accent colour and
turns white on hover or when chosen. The Control Kit shows every state of each.

Chrome — tabs, buttons, headings and labels — is set in Saira at normal width instead of Saira
Condensed, and prose in Sintony, Elite's own body face, instead of Titillium Web. Letter-spacing
on capitals is much tighter, and names, values and sentences are not spaced out at all. A screen
title is white, no longer glows, and sits over an accent-coloured rule. Section headings are
smaller, with the rule beneath them instead of beside them. The smallest text is 12 instead of 11.
A settings row label too long for its column wraps instead of running under the control beside it.

Text and number fields have a thin accent-coloured outline on all four sides, which turns cyan
while you type in the field, with white text on no background. Units beside a number read in the
accent colour. Icon buttons, such as reset and a number's up and down arrows, are square tiles that
fill solid on hover or when reached with Tab, and a label naming the button appears beside the tile
straight away. They no longer have a tooltip, and no longer glow.

Every on/off switch is now Elite's checkbox: a small square with an accent-coloured outline, filled
with a smaller solid square when ticked. The box and its label form one tile row that turns
lighter on hover or when reached with Tab, and clicking anywhere on the row ticks it. Space and
Enter tick it too. Settings rows, the Transcript's Raw switch, the page filters and the checklist's
completed box all use it. A checkbox you cannot change has a grey outline.

Every d47 window has a 1px border on all four sides, so its edges show on a black desktop: the main
window and the Control Kit in a dark brown, dialogs in the accent colour. The border changes with
the theme and is not drawn when the window is maximised. The title bar is one shade lighter than the
page, with a thin rule beneath it on every window. The panel's content sits closer to the edges:
20 at the top, 28 at the sides and 24 at the bottom, in place of 32 all round.

The minimise, maximise and close buttons in every title bar show an accent-coloured mark on no
background. Minimise and maximise fill solid in the accent colour with a dark mark on hover, on
press or when reached with Tab; close fills solid red with a white mark. Reaching them with Tab
used to show nothing.

The confirmation dialog and the "What this has cost" dialog share one layout. An orange context
line and a white title sit above an accent rule, the body scrolls, and the buttons sit beneath a
thin rule. The cost dialog shows the session's running total at the top right. Esc closes both;
closing the confirmation this way answers no. Loadout's power and jump range gauges are a thin
solid bar in place of the thick striped one. The bar is orange, and red when the build does not fit.
The gauge's name is grey and its reading is in the bar's colour. The Control Kit has Stat tiles,
Gauges and Modal sections.

The Transcript's In Ship page is laid out like a text conversation. D47, Tower, Carrier and every
other in-ship voice sit on the left behind an accent-coloured bar. Your own turns sit on the right
behind a cyan bar on a faint cyan ground. A turn is as wide as its text, up to 80% of the list, so
long turns from both sides overlap across the middle and short ones stay narrow. Hovering shades a
turn grey. Each turn is headed by the speaker's name in capitals, in the bar's colour, then its
source and delivery note in grey, with the time at the right. A delivery note such as [calm] is
shown in the heading and no longer in the text. System name chips are grey tiles, cyan for the
system you are in. The microphone dot is cyan when push-to-talk is ready. The footer has no
background of its own, and SEND and the Newest button are ordinary tiles.

Fleet's ship pages are restyled to match Elite. Ship cards, slot rows and material rows are tiles
2px apart with a white name and an accent-coloured second line. The card or row the other pane
is showing fills solid in the accent colour instead of being outlined. The plan dot and the
engineered gear take the second line's colour, so they can be read on a hovered or showing row.
The CURRENT SHIP badge is cyan. A ship's page opens with its name as a large white title over an accent rule. Its figures
(hull, pad, speed, boost, armour, shields, price, jump range, hold, mass, value, rebuy and hull
integrity) are grey-labelled tiles that drop columns as the pane narrows. Where the ship is reads
in cyan when it is the ship you are flying. Headings such as Fitted, Planned and the slot groups
are white capitals over a thin accent rule, on the Suits pages too, and "Say:" hints are grey. A
met plan's tick is blue. Materials no longer scrolls inside five fixed-height cards: each group is
as tall as its rows and the page scrolls. A material you hold enough of reads in blue, a shortfall
in the accent colour instead of red, and its detail opens in the same layout as the confirmation
dialog. Scrollbars on Fleet and Engineers pages sit clear of the content instead of over it.

Fleet › Carrier is restyled to match. The carrier's name is a large white title over an accent
rule. Its system, jump range, space, cargo, balance, docking and services, and the tritium figures
under a TRITIUM heading, are grey-labelled tiles that drop columns as the pane narrows, with COPY
inside the system tile. A carrier in the system you are in shows that system in cyan. The
squadron's carrier sits under a white capitals heading with its own tiles, and "Booked for
decommissioning" reads in red. A suit or weapon's page now opens with its name as a large white
title, and a material on its costs list that you hold enough of reads in blue. Tiles in one row
of any Fleet page are now all the same height.

Engineers is restyled to match. The Directory's group headings are white capitals over an accent
rule, and the marker on an engineer you have not unlocked is red. The two Hide checkboxes wrap
onto a second line in a narrow pane instead of being cut off. An engineer's page opens with their
name as a large white title. Their workshop, with COPY inside it, the distance and where you stand
are grey-labelled tiles, and the workshop reads in cyan when it is in the system you are in. Grades
read in the accent colour, and planned work and each stop on the way in sit on tiles. Each
prerequisite sits on its own tile and reads ✓ MET in blue, IN PROGRESS in the accent colour with
its bar, ? UNKNOWN in white or NOT MET in grey. On the Route, each ranked engineer is a tile with
their name in white and the ranking's summary under it, and pressing it opens them.

The Checklist is restyled to match. It opens with a large white Checklist title over an accent
rule, and Suggestions does the same. Each line and each goal is a tile with its name in white and
its second line in the accent colour. The selected line fills solid in the accent colour instead
of taking an outline. Its move, Edit and Delete buttons sit on a line of their own under the text
and wrap in a narrow pane instead of being cut off. A finished goal or plan line is marked ✓ in
blue, and a goal's bar is the accent colour on a tile track. DONE is a white capitals heading over
a rule. Delete completed items is a red button at ordinary button height, as is Delete on a line.
The filter row wraps in a narrow pane instead of cutting Delete completed items off. On the mini
strip the title is left out so the list has room. On every tab that has one, Settings for this
page is an ordinary button with its count in brackets.

Routing is restyled to match. Each page opens with a large white title over an accent rule: Plan,
Course, Market, Community Goal and Trade route by name, Progress by the route, and a plan by its
summary. The cards are gone. Each form sits under a white capitals heading over a rule, with its
HELP on the heading's line, and Plan's two planners sit side by side where the pane is wide
enough. Form fields, switches and buttons wrap in a narrow pane instead of being cut off, and
each switch is only as wide as its label. Every hop, waypoint, stop and market result is a tile.
A system name is in the accent colour, your current system is cyan, and a system you have
already passed is grey. A reached stop on a plan is marked ✓ in blue, and the next one reads
NEXT. Hazards read in red capitals and scoop warnings in grey capitals, with no box around them.
A price you saw yourself reads in cyan. The Community Goal ledger's session, day and week are
tiles, and a mistake in a form, such as a missing destination, reads in red. On a market result,
COPY no longer overlaps the Distance column.

Adventures is restyled to match. The tab opens with a large white Adventures title over an accent
rule, and a story, Write, Edit and Ask each open with their own. Each story is a tile with its name
in white and where it stands in the accent colour, and the cards' outlines are gone. In a trigger,
your current system is cyan. Decline and Remove are red buttons. The editor's spine and beats sit
under white capitals headings, each beat is a tile, and an empty field reads in dim grey. The
editor's field buttons are as wide as the longest label rather than cutting labels off, and the
caution under the beats wraps instead of running off the edge. Button rows wrap in a narrow pane.

Settings is restyled to match. The area you have chosen opens with a large white title over an
accent rule. The cards' outlines are gone: each card's name is a white capitals heading over an
accent rule, and each group inside it a smaller white capitals heading over a thin rule. Rows no
longer alternate in shade. Each row sits on the page background under a thin rule, so the
steppers, choices and buttons in it still show as tiles. Helper text and the protected-rows note
read in grey, and a read-only value such as what leaves this machine reads in the accent colour.
A chosen voice or model reads in the accent colour and an unset one in dim grey. A stored key reads
KEY STORED in yellow and a missing one NO KEY in grey, and a key that checks out reads in blue.
PER COMMANDER is grey capitals with no box. The rows use more of a wide window, and a row's help,
when a search brings it out, wraps beside the control instead of running across it. While
searching, each area's name is a capitals heading over a rule. The first-run key window matches.

Utilities, Learned phrases and the in-app help are restyled to match. Each opens with a large white
title over an accent rule; on the small panel Utilities leaves its title out so the list keeps the
room. The two clocks are grey-labelled tiles with the time in the accent colour, and each timer,
alarm, learned phrase and help link is a tile row with a white name. Forget is red. Help's section
headings are white capitals over a rule, and its prose is grey.

Every other dialog now has the same layout as the confirmation and cost dialogs: an orange line
naming where it belongs, a white title over an accent rule, and the buttons beneath a thin rule.
Esc closes each one, and cancels the picker. This covers the audio recorder, What changed,
Exercised by hand, the debrief, Help improve D47, the Commander's log, lore, macros, memory, your
own cores, HOTAS switches and the picker. Headings inside them are white capitals over a rule,
helper text is grey and problems are red. Saved entries are tile rows, and entries you edit sit
under a thin rule so their boxes still show. Forget, Discard, Withdraw, Remove and Delete are red. Help
improve D47's four counts are tiles, and the exact text sits on a darker ground with no outline.
In Exercised by hand, done reads blue, failed red, and never and changed grey, in capitals. The
recorder fills the row you picked in the accent colour. Rows of buttons and choices wrap in a
narrow window instead of running off the edge.

## 1.6.3 — Guardian theme removed

The Guardian theme is gone from Theme's choices. A saved theme of `guardian` now opens as Elite,
with no error. The Guardian voice and Guardian cores are unaffected.

## 1.6.2 — Controls drop their ornaments

The Interface capability now has a Bloom setting, beside Theme: 0 to 2.5, default 1.1. Changing it
resizes every glow halo in the open panel and the headset overlay without a restart. On Light the
row shows disabled, since bloom never draws there.

Panel tab labels are now in capitals, and the selected tab shows its whole label in bold. On a
150% display the selected tab had lost its last letter and read "Transcrip".

Scroll bars now take the d47 theme instead of Fluent's grey default: a thin D47.Rule thumb on a
transparent track, turning D47.Accent on hover or drag, with no arrow buttons.

Every corner in the app now draws square. The settings card header, the Control Kit's primary
button and the panel's level-1 tab keep the HUD's one clipped corner and one shear; every other
rounded or clipped corner named in the design audit is gone. The headset caption box now takes its
ground and ink from the selected theme instead of a fixed dark box and near-white text, so a
caption stays legible whatever palette is on.

The Debug-only Control Kit window now follows the design reference's layout and wording. It opens
with the CONTROL KIT title, the reference's introduction and three spec cards (Spacing, Row,
Columns). The ten controls sit in a three-column grid in the reference's order, each captioned with
what kind of control it is and ending in a one-line note. The grid drops to two columns, then one,
as the window narrows. Theme comes before Status. Status shows five full-width bars, one per
colour, each labelled on its own fill. The four heading ranks share one left rule, the window
carries the panel's scanlines on dark themes, and the filled field takes focus on open so its
block caret shows.

Drawn pictures on buttons are now words. Copy is COPY, and says COPIED or COPY FAILED after a press;
the banknote is SPEND; the plus on the checklist is ADD; the settings page's plus and minus are
EXPAND ALL and COLLAPSE ALL; the `?` marks are HELP; the Loadout card's `ⓘ` is SOURCES. The hull
picture's size marks are a BESIDE / WIDE choice with ZOOM beside it, and the headset's resize bar
reads ZOOM OUT, ↺, ZOOM IN and DONE. An engineer's prerequisites read MET, NOT MET or UNKNOWN in
place of a ticked, empty or dashed box. Reset is the ↺ character, in a 44 × 44 target everywhere
it appears. The Help Improve window's `ⓘ` is gone; its reasoning is the hover on the window's
opening sentence. The window's minimise, maximise and close buttons are unchanged.

The Transcript is rebuilt to the design reference. The panel has no frame of its own: the window
edge is its edge, with 32 pixels of padding. Every message now sits on the left behind a thin rule,
under a badge naming the speaker: D47 in the theme's colour, CMDR in grey, a persona or a role such
as Tower in between. The Commander's messages are no longer on the right in a box of their own.
Message text is dimmer and wraps at a readable width, and machine text inside a message is in the
monospace face. Hovering a message shades the row behind it. An empty conversation shows one
line saying nothing has been said yet, and resizing the window keeps the newest message in view
unless you have scrolled up. The search field sits at the right of the readings row,
with a CLEAR beside it while there is a query, and a rule under the row. The microphone line and
the ask box share one footer: the dot beside PTT READY, LISTENING or MIC ON is the only thing that
changes colour, and it turns the danger colour when no microphone is open. The ask box grows to
three lines as you type, then scrolls, and its send arrow is now a SEND button.

A settings problem at startup now speaks only "My settings did not load cleanly." The path and,
for a kept unknown key, the key names stay out of speech; the window and the log still show the
full detail.

## 1.6.1 — Every hull has its figures

A hull missing from the shipped table now names itself and says which figures are missing —
"I have no hull figures for the Corsair: its speed, boost, armour, shields and cost are not in my
table." — rather than "I have no figures for this ship," which read as though d47 knew nothing
about the ship even while the game's own jump range, hold, mass, value and rebuy were shown below
it.

## 1.5.1 — Tooltips say only what the screen does not

A gauge's name now draws in uppercase mono at the left edge, and its reading right-aligned in mono,
both in the gauge's own tone colour — in the Control Kit's three demo gauges and the Loadout page.
The name drew in TextMuted SemiBold before, with the reading following it in the prose face.

Tooltips draw square, opaque and un-Fluent: a fill-3 ground, a 1px rule border, no corner radius
and no shadow, in the prose face at 14pt, wrapping at 280px. They drew as Fluent's rounded gray
card with a drop shadow before. Every tooltip opens 600ms after the pointer settles, below and
left-aligned with the control it explains, 8px away, instead of each call site setting its own
delay.

A tooltip now survives in four cases only: a bare glyph names the action it takes, cut-off text
carries its full value while the column actually clips it, a disabled control says why, and a
Settings row's label shows the row's own explanation on hover or focus, as plain text with nothing
clickable inside it — the card heading's `?` is the only way from there to the help page. Gone
from everywhere else: the Stepper's own tooltip repeating the value cell 20px away, `Default: …`
on a chooser, number box or text box, the Settings tab naming its own shortcut, worded buttons
such as Copy All, Raw and the picker's default button repeating their own label, a help mark
showing the address it is about to open rather than saying what pressing it does, the pre-release
badge's and the donate button's own explanations, the microphone row's detail, the adventures
ship-only note, the hull picture's controls, and a route row's "Copy …" hint. Every `↺` reads
"Reset to default"; a screen reader still hears which one. The Stepper's value cell, a chooser
button's label, the Loadout tab's slot and side-cell names, and the coverage report's item name
now carry their full text only while the column has actually cut them off. A picker's play glyph
now names the action it takes as well as the cost, where it has one.

The Loadout page now shows speed, boost, armour, shields and hull mass for the Corsair, Caspian
Explorer, Kestrel Mk II and Lynx Highliner. Each was missing them: the first three because
coriolis-data's own id for the hull disagrees with FDevIDs', and the Lynx because coriolis-data
has no file for it at all. The generator now falls back to EDSY's figures, keyed by the symbol the
journal writes, wherever coriolis-data's id does not join.

The build page's Jump range gauge now draws for any hull the shipped table has no mass figure for,
once a plan changes a module. It worked out the hull's mass from the table before, which for a
hull missing there meant no gauge at all. It now takes the hull's mass from the journal instead —
the ship's own reported mass less every fitted module's — and only falls back to the table when a
fitted module's own mass cannot be told either.

## 0.169.0 — No more drop-downs

Screen titles glow in Elite, Dark and Guardian, matching the primary button, the lit switch cell,
the selected segment and the tab. They drew with no glow before.

The glow on dark themes is drawn as several layered halos, from a tight bright edge out to a wide
faint wash, at two strengths. The window title's diamond and name, Screen titles, the Level's fill
and handle, and the microphone dot take the stronger; the selected tab, the primary button, the lit
switch half, the selected segment and a pressed glyph button take the other. It was one 10px halo
before, and on a button, tab, switch or segment it was cut off at the control's edge. The
microphone dot glows in its own colour whenever it is filled, and the microphone row no longer
glows as a box while the gate is open. Light still draws no glow.

Subgroup headings draw in the mono face at caption size, letterspaced as wide as the Control
Kit's cell captions; row headings draw in the prose face at body size, in sentence case, matching
the settings row labels. Both used the chrome face and — for row headings — upper case before.
Checklist, Loadout, Route, Adventures and Settings headings all follow.

Stepper draws solid `◀`/`▶` arrows and sets its value in the prose face at body size, with an 8px
gap between the box and the position/consequence line underneath. In Settings it now shows at full
height; it was held to 32px there, which cut off the bottom of its 44px box and the whole line
underneath.

Switch labels read ON and OFF in bold, letterspaced uppercase at the Segment's size, matching the
Segment control's chrome instead of the plain "On" and "Off" they drew before.

The level-2 text row reads uppercase — the Control Kit's "In Ship", "Log File" and "Journal File"
included — keeping its existing letterspacing.

Text fields centre their text and placeholder vertically at body size in the prose face, instead
of sitting against the top edge at the default size.

Binding chips draw at normal weight with no letterspacing, matching the mono face they set
instead of taking the button style's bold, spaced-out chrome.

Report rows draw their text in the prose face at body size, matching the reference. They read at
Secondary size in muted ink before, which looked disabled.

Normal and Destructive buttons draw their 1px border again. The Button template bound
`BorderThickness` into `ChamferedBorder`, whose own property was a `double` where a Button's is a
`Thickness`; the binding failed silently and no button anywhere in the app drew an outline.

A Debug-only Control Kit window (Ctrl+Shift+K) shows every control theme and resource key
against the derived colour ramp, the heading ranks, and the five app themes, drawn from the real
controls rather than copies. Its Accent entry recolours the app through the same HUD-matrix path a
Commander's own matrix change takes.

The app no longer crashes on launch. Three `FocusAdorner` setters — on the Button, TextChoice and
Slider themes — referenced a `ControlTemplate` that the XAML compiler left unbuilt against a
`FocusAdorner` property, throwing `InvalidCastException` on the first control that applied one of
those themes. The focus ring is now built in C# and referenced with `{x:Static}` instead.

Screen and window titles draw hot; group headings draw nowrap with a trailing rule; subgroup and
caption headings draw faint. The Settings sidebar's active item is now a solid Accent fill with
Knock text, and an inactive item draws in muted text. The protected-row legend now names the bar
it means.

Tabs show their word alone, with no icon. The active tab is a solid Accent fill with Knock text;
the rest sit on the higher fill in faint text. Both are sheared 13px. A strip too narrow for every
tab now wraps a tab onto a second row instead of clipping it or scrolling to reach it.

A new Level control draws the handoff's settable slider: a 30-tall track with a solid Accent fill
inset 3px, blooming on dark themes, and a 10-wide handle overhanging the track 5px top and bottom
inside a 44-wide hit area. The numeric value always shows beside it, and arrow keys step by one,
Page Up/Down by ten. Nothing in the app uses it yet.

Every reported bar on the Loadout pages now draws as the hatched Gauge: 14 tall, square-cornered,
with muted end caps and a hatched fill in the reading's own colour, instead of a rounded solid
bar.

A page's readings — In Ship, Log File and Journal File on Transcript, and the rest that switch a
page's own view — now draw as a plain text row instead of a segmented pill or a stepper. The
current reading is hot text with a 3px Accent underline; the rest are faint with none. A row too
narrow for every reading wraps onto a second line instead of stepping through them one at a time.

A page's own "Settings for this page" strip now sits at the bottom of the page instead of the
top, on Fleet › Ships, Fleet › Carrier, Adventures, Routing › Community Goal and Transcript › Log
File, on the desktop window and in the headset. An open strip never takes more than half the
page's height; longer ones scroll inside the strip instead of pushing content off the bottom.

The mouse's Back button now navigates the panel back, on the desktop window and the SteamVR
overlay, wherever the pointer sits over the panel. The Forward button does nothing.

The Engineers tab and the Fleet build page now name the Radar and Main Engines slots the way the
outfitting screen does: Sensors and Thrusters. Armour is unchanged.

Fuel low and fuel critical no longer speak on a stale tank level. Right after a ship swap,
Status.json can still report the old ship's fuel for up to a second while the new ship's tank
capacity has already applied, reading as a low fraction that was never really low; the callout now
also checks Elite's own low-fuel flag before speaking.

Help improve D47 now leads with one sentence and one primary button, Send it, instead of eight
buttons of equal weight under five paragraphs. Three consent lines replace the old bullet list;
what will leave is shown as four figures — log entries, journal events, names replaced,
characters — that update as the switches change, with no press. The exact text stays collapsed
behind a disclosure until pressed, and a quiet link goes to the full privacy note on the site.
Save and copy are quiet, Cancel is faint, and Forget is destructive.

A button now has two weights below primary: quiet, with no border and muted ink under a dotted
underline, for an alternate or an exit; and destructive, with a Danger border and ink, for a
delete. The primary weight's fill now clips its top-right corner.

A toggle switch now carries its own words — "On" and "Off" by default — on two halves that fill
and dim as the switch changes, instead of a knob whose only signal was position and brightness.

The Elite, Guardian and Light themes now use the handoff's own accent and background colours
(`dark` is unchanged). Text and glyphs sitting on a solid accent fill — the primary button, a
switch's On half, a checked segment, a checked tab — now use their own colour instead of borrowing
the theme's background colour, so Light reads correctly there instead of showing dark-theme ink.

Bloom and scanlines now match the handoff's own numbers on dark themes: one glow, at 10px and 34%
with no offset, on the diamond mark, the window title, the active tab, a switch's On half, a
checked segment, the primary button and the microphone status row; and a black scanline at 34%
alpha, the whole overlay at 55% opacity. The tab-strip rule, the panel's outer edge and the
selected list row no longer glow. The headset no longer reads a stronger bloom than the desktop.

Chrome text — the window title, tab labels, group headings, button labels and the settings
sidebar — now draws in Saira Condensed instead of Saira. Prose and row labels — settings rows,
placeholders, message bodies — now draw in Titillium Web instead of Saira Semi Condensed. Tab
labels, group headings and the window title carry the handoff's own letter spacing.

The four button weights now carry the handoff's own sizes, padding and ink. The normal weight
draws no fill and its ink in the plain text colour, not accent; primary is bolder and larger, with
its ink in the new knock colour; quiet's underline is dotted instead of dashed; destructive draws
no fill either. Hovering the normal weight switches its border to accent; hovering quiet also
switches its ink to the plain text colour; destructive and primary keep their own colour on hover.
A disabled button now dims to 35% opacity instead of recolouring. Every button also carries a 2px
accent focus ring, 2px clear of its edge, on keyboard focus.

A segmented choice now draws as one framed group on its own ground, 3px padding and 3px between
segments, wrapping onto a second row rather than clipping or overflowing when it runs out of
width. Segments carry no border of their own; the unselected ink is the faint text colour, and the
selected segment still carries the accent fill, the knock ink and the bloom. Arrow keys now move
the selection.

A stepper now carries the handoff's own numbers: 44 tall with 44-wide arrows and no ground of its
own, and a value cell filled in the stronger accent tint with its ink in the new accent-ink colour,
ellipsising only past 512px. Its position and cost line below now draws at 12px mono, a size the
type scale gained for it.

A number setting now shows its unit inside the box, in a small chip beside the value: "Capture
before the key" reads 500 and ms rather than naming milliseconds in its label, and the same goes
for the other Listening timings and the speech margin in decibels. The box is 44 tall, the value
is in mono, and the up and down arrows sit in 40-wide cells that repeat when held. Focus now
underlines the value instead of lighting the whole frame.

A text field now pads 13px left and right instead of top and bottom, its placeholder reads in the
faint text colour, and its caret draws as a 9x21 Accent block instead of a thin line.

A bind row with two bindings now shows one chip per key instead of joining them into a single
button's text, each in mono on a filled ground with a rule border, wrapping onto a second line
rather than overlapping. Unbind is now a quiet-weight CLEAR beside the chips.

A protected settings row now carries a left bar instead of a bordered "protected" chip, with one
legend line under the screen title on any screen that has one. A row's help now shows on hovering
or focusing its own label rather than behind a separate info glyph.

A settings row now puts its control immediately beside its label instead of at the far right of
the row, and reserves the same width for the reset button whether or not that row draws one, so a
card's rows no longer go ragged down the page. The row's minimum height, padding and every margin
in the settings view now come from one spacing scale.

The settings panel's text now reads at seven sizes instead of five, grown for the headset — body
text moves from 14 to 16. The reset and info buttons on a settings row, the stepper's arrows and
the number field's arrows now offer at least a 44x44 target for a VR ray to land on.

A stepper now shows where it stands in its list and what stepping onto its next value costs, in
mono under the arrows — position on the left, cost on the right where a row has one. The speech
model row names each model's size, whether it is English only and how it compares for speed;
holding an arrow now repeats the move rather than requiring a press each time.

Machine text — the live log, the journal list and detail, the Help Improve payload and excerpt
preview, and time and key readouts — now draws in an embedded JetBrains Mono rather than
whatever monospace font the machine happens to have installed.

A layer chooser — the module, keybind and setting pickers that open over the current page rather
than taking the panel — now dims the page behind it instead of letting it show through or covering
it edge to edge. The card sits centred and capped in width over that dimming, the search box and
page bar hide while it is open and come back once it is dismissed or abandoned, and a list longer
than the card scrolls inside it.

A read-only settings row — "Data folder", "Version", every other read-out — now draws with no box
at all, just a rule on its left edge, instead of the same bordered rectangle a text field draws.
An editable text field now sits on a lit ground with one bright edge on the bottom rather than a
box on all four sides, so the two kinds of row are told apart at a glance.

Owned suits and weapons now survive a restart, the same way the fleet already did: bought,
upgraded, sold and equipped items are kept in a per-commander file and restored on load, merged
under whatever the current session has already seen. A suit or weapon bought and not worn since is
now found by walking older journals as far back as the stored file needs, rather than only the
most recent ones.

The Suits page now lists every suit and weapon you own, not only the one Elite currently reports
you wearing or carrying. An owned but unworn item shows its recorded grade and when it was last
seen, and opening its Fitted section says the same rather than "I cannot say".

The exploration goals — systems visited and distance flown — are gone from the Goals section of
the Checklist tab. Both counted a milestone this repository picked rather than anything Elite
states.

The keep-or-delete, keep-or-abandon and keep-or-remove confirmations no longer mark either option
"fitted now" or "chosen now" in bold. Neither answer is current until the Commander picks one.

Two more arcs are in the Goals section: Imperial Navy and Federal Navy, running to King and
Admiral rather than to Elite. Each reads its rank's name — Serf, Cadet, and on up each ladder —
the same way a career arc already does.

A Powerplay rank arc joins them, but only while you are pledged: it reports the rank you hold
against 100, appears when you pledge and goes again when you leave. A promotion moves the figure
straight away rather than at the next startup.

The Checklist tab's Sourcing page is gone and the tab has one root again. Asking what a
construction site still needs, and where to buy it, works as before — it is a voice answer either
way — but a figure you typed for what is already on your carrier is no longer taken off the
shopping list, and there is nowhere left to type one. Colonisation sourcing is being redesigned.

The Suits page now draws cards, like the fleet page: kind and grade, where it is, and how many
slots are planned, each its own line, with a badge for the one you are wearing.

Checklist grade and modification lines now read the same way: a step on a suit or weapon you own
but are not wearing now reads Open with the recorded grade and when it was last seen, instead of
saying nothing at all.

"Get clear and supercruise", "boost and warp" and the rest of that pattern now route to separate
and supercruise, the same way their jump equivalents already routed to separate and engage.

Putting a suit or weapon plan on your checklist now leaves a question on the Suits page itself,
the same way it already did on the Ships page, instead of only being answerable from the
Checklist tab.

Planning a suit or weapon you do not own, and planning a modification on one you do, now offer a
list to pick from instead of asking you to type or say it blind. The suit and weapon list excludes
the flight suit, and a modification list excludes anything already planned on another slot of the
same build.

Invented background chatter no longer mentions a mail slot at a station without one — a fleet
carrier, an outpost, a surface port or a settlement, among others. Only Coriolis, Orbis, Ocellus,
Dodec and asteroid bases actually have one.

The carrier's captain and tower can now be given their own names. Set them on the Fleet › Carrier
tab, where their voices moved to as well — the settings window's Carrier voices place is gone. A
named captain is addressed as "Captain {name}"; a named tower replaces "Tower Control" with the
name given. Leave either empty and the line reads exactly as it always has.

The Carrier tab's single Tritium row is now five: in the tank, in the carrier's own hold (marked
"counted", or "may be off" while a tritium trade order is open), in your own ship's hold, a total,
and a rough range — the tritium spent to jump follows the carrier-jump formula, based on a spansh
plot of Sol to Colonia, and ignores the carrier growing lighter as it burns fuel, which is why it
is called rough. The page now redraws when your ship's hold changes, not only when the
carrier's own figures do.

The Carrier tab now survives a restart: fuel, cargo, capacity, free space, jump range, balance,
docking access, decommission status and crewed services are folded from history the same way
location and identity always were, dated by the same "Figures as of …" line. Only a jump already
under way is still forgotten, since the tank drain since the last figure is not tracked either.

Every drop-down is gone. A choice of two to four is a row of segments; a longer list, or one that
can grow, is a stepper with an arrow either side. Nothing opens a list that can close under a VR
pointer, so every choice now works on the headset panel directly.

Moving through a choice no longer does anything costly. Picking a ship to plan opens a list you can
narrow by typing; pressing a row or an arrow key only highlights it, and nothing is planned until
you press Plan this hull or Enter, or say the name. The speech model and the local voice build now
stage the one you step to, and the one in use keeps running until you press the button that names
the cost, such as "Download 466 MB and use it".

The conversation bubbles take the HUD dress too: Saira prose instead of the monospace font, a faint
tinted fill and a chamfered corner on the tail side — Accent for the ship, Info for the Commander —
in place of the rounded corners and flat Accent fill they drew before. The Log File and Journal File
readings still draw monospace.

Your own carrier's routine traffic is now always addressed to you as its owner. Lines Frontier wrote
for a visitor, such as "Ensure to observe starport protocol during your visit, pilot.", are always
reworded, whatever the reword percentage is set to. When no model is set up, Personality is off, or
the model fails, times out or returns something unusable, d47 says its own line instead, for
example "Welcome back, Commander Seelinger." The log now records at Information level whenever a
line was spoken as written, and why.

The carrier's tower and captain no longer say the same sentence every visit. Each of the six lines
they speak as written — the ship secured, the captain's welcome, clearing the deck, the tower
telling the captain the Commander is inbound, the captain's answer, and a jump plotted — now draws
from a pool of six, cycling so two visits running never repeat one. When personality is on, the
model is asked to compose in the same situation rather than reword a fixed sentence, the same way
the docked welcome already did. Overheard tower traffic at a station or at your own carrier now
varies its subject and its opening beat too, instead of always being "clearances, pad assignments,
a telling-off".

The Fleet cards do too. A ship's hull picture now sits in a pure black cell with a rule beneath it
in every theme, the current ship carries its badge in the corner rather than a highlighted card, and
the name is drawn upper case in Saira Semi Condensed with the hull, where it is and what is planned
each on their own line beneath it. The card the other pane is drawing now reads by fill as well as
by border — 14% of Accent and a solid edge, against 5% and a rule for the rest. "Settings for this
page" is now a chamfered strip with a chip counting its rows, on the Fleet, Routing and Adventures
tabs alike.

The Settings page takes the dress too. The section tree's selected node carries a 3px Accent bar
and an 18% fill; the top-level areas draw upper case and tracked, the places under them in Saira
Semi Condensed, indented and inked in Accent. Rows are at least 60px tall and alternate on a 5%
Accent fill, and the reset glyph moves off the label and onto a square 40x40 button at the end of
the row. The protected and per-Commander tags carry a 60% Accent border and no fill, and a bound
key now reads in monospace. No row or tree node draws a rounded corner.

Every other hand-built card takes the dress as well — Utilities, Adventures, Learned Phrases,
Community Goal, Market, Route Plan, the Loadout notice and drag ghost, the macro, persona, switch,
coverage, debrief, logbook, lore, memory and audio-recorder windows, the entry-prompt card, and the
headset's own overlay card. Each draws square with a 5% Accent fill and a Rule border, or 14% Accent
and an Accent border when selected or open. The current row on Route progress and the next jump on
the plan result now carry the same 3px Accent bar the Settings tree uses. The macro, persona,
switch and picker windows no longer paint their whole background Surface; they take Background,
like every other dialog. The headset's zoom glyphs and the offscreen page's row buttons draw as
the kit button now, dropping the overrides that used to sit on top of it.

The Conversation page now says who is speaking. Every bubble but the panel's own note carries a
head: a chip naming the speaker — CMDR, D47, a persona's own name, or a role such as Tower or
Carrier for a callout with nobody specific behind it — a small tag naming the callout it came
from, and the time. Callouts spoken to you now join the conversation the same way a reply does;
invented chatter and a message you only overheard stay off it. The bracketed name in front of a
persona's reply is gone now that the chip carries it.

A checklist proposal now shows as a card in the Conversation, tagged proposal, with its summary
and its own Accept and Decline. Settling it — from the card, from the Checklist page, or by
saying "accept the proposal" — takes the buttons away and settles both: the card reads what
happened and its tag changes to proposal · accepted or proposal · declined, and the proposal
leaves the Checklist page's own list the same moment.

Every text box and number field takes the dress too: square corners, no fill, and a border that
turns Accent on focus in place of the rounded grey box Fluent draws by default. Page and card
titles — the Fleet, Engineers, Checklist and Adventures headings, the route plan cards, and the
Settings page and its section titles — now draw upper case and tracked in Saira Semi Condensed;
a line that reads as a sentence, such as a route summary or a prompt's question, keeps its own
case and takes only the typeface. The breadcrumb row now draws in Saira Semi Condensed as well.

The eight per-subsystem log levels move to the Log file page, as that page's own settings strip:
the default level, and a track below it with one row per subsystem — seven stops from None to
Trace, a readout, and a reset. A row with its own level draws in Accent; a row with none follows
the default, shown as a hollow marker that moves when the default does. The Diagnostics card on
the Settings page now carries only what is paused and hand-testing coverage.

SteamVR's own account of a panel's position and visibility now writes at Information only on the
first sighting and on a real change; the five-minute heartbeat that repeats an unchanged
description now writes at Debug, so a still panel no longer pushes real log lines off the page.

The microphone indicator moves to its own box above the ask line, styled to match the HUD kit —
a faint Accent fill, a rule that turns solid Accent while the gate is open with a glow behind it,
and a round dot in place of the microphone glyph. It stays on screen with the microphone off,
reading MIC OFF rather than disappearing, and it survives mini mode and the headset even though the
ask box does not. The ask button loses its own fill and rounded corners for the kit Button style,
and the ask box's placeholder now draws upper case and tracked, in Saira Semi Condensed, at heading
size; what you type keeps its own case.

The adventure editor no longer draws an unfinished draft in Danger. What is missing now reads as
one caution line — an Accent bar, a CAUTION label, and a sentence naming everything still needed,
such as "An adventure needs a key, a name, and at least one beat before it can be saved." A beat
whose place is not yet resolved carries the same Accent ink rather than red.

Elite, Dark and Guardian now carry a bloom and scanlines; Light carries neither. The tab strip's
unselected label, the selected tab's own fill, the selected list row's leading bar, and a primary
button's fill each carry a soft Accent glow, and a faint repeating line sits over the whole
window, on the desktop and in the headset alike.

The Elite theme's ground is now black, and the Conversation's pane is black with a faint Accent
tint, where it was grey. The ship's prose is a lighter amber, and a callout's key is plain
monospace text instead of a boxed tag. The time is monospace too. A short conversation now sits at
the bottom of the pane, beside the ask box, instead of at the top. In Ship, Log File and Journal
File are three segments rather than a stepper, and go back to the stepper on a pane too narrow for
all three. The line saying which path answered is no longer monospace. The scanlines keep their spacing at every
display scaling, so they stay visible at 150% and 200% and stay sharp at 125%.

Bloom is recalibrated. The unselected tab's label no longer glows — it sits on a tinted fill, and a
glow only shows where the area around it is dark. The primary button's fill, the selected tab and
the selected list row's leading bar now glow stronger in the headset than on the desktop window. A
new rule sits under the tab strip with its own glow, and the panel's outer edge now glows too.

Every window but the VR overlay now draws its own titlebar instead of the Windows one: the app
icon and title on the left, minimise, maximise and close on the right, in d47's own type and
colour. Dragging the strip moves the window, double-clicking it maximises or restores, and Aero
Snap, edge and corner resizing, and Alt+Space still all work as before. A window that cannot be
resized shows no maximise button, and the picker window — which has no taskbar entry to bring it
back from — shows no minimise button either.

An upgrade no longer reports settings it does not know. When a newer release first starts, it copies
`settings.json` to `settings.json.<old version>.bak` beside it and deletes any key the new build does
not use, without a startup message. A key typed by hand after that, or one left by a newer build on
a downgrade, is still kept and named.

A planned engineering slot no longer reads "I do not know your rank" for every entry on the Gap
page. The recipe is now resolved by the module actually in the slot — the plan's own module, or
what is fitted, when a blueprint name such as Heavy Duty belongs to more than one module type — so
a Shield Booster and Armour cost their own ingredients rather than whichever the table lists first.
Where a module cannot be told apart, the slot is left uncosted with a note saying so. The total now
uses the highest rank among the engineers who could actually craft it, and where nobody unlocked
reaches the grade yet, it is counted at the most rolls the grade can take, with a line saying why.

The Fleet tab's Gap page is now Materials: a two-position switch for Ship and On foot, then every
catalogue material as a card — held and needed, not only what is short. A card's ⓘ names where its
materials come from, and clicking a row opens its detail in the page itself, never a pop-up window,
so the headset can open it too. The filter for hulls and suits not yet bought is gone; the page
always counts everything planned.

"What do my plans still need" now opens "You're short 85 units of 4 materials, for 12 ships and
suits you've planned." instead of naming only the units and the plan count, so the answer says what
it is counting.

A trade run's leg limit is now a number of jumps rather than a light-year figure. `plot_trade_route`
takes `max_jumps` (1 to 10, default 2) and an optional `jump_range` in place of `max_hop_distance`;
where the laden jump range is not given and cannot be worked out from the flown ship, the tool asks
for it instead of guessing. Each stop after the first now says how many jumps it took, and the
"Longest leg (ly)" box on the Trade run card is now "Most jumps per leg".

`plot_trade_route`'s default hold is now the ship's cargo capacity minus any limpets already
aboard, since a trade run never sells them. A new `planetary` switch, off by default, brings
surface stations into the search; the reply now always says the hold the route was planned with.

Trade routes no longer stop at systems you need a permit to enter. `avoid_permit_systems` is on by
default, and the station the Commander is docked at is exempt so a plan can still start from Sol or
Shinrarta Dezhra. The station index carries no permit field, so the rule is answered from a new
shipped table of 2,702 permit-locked systems, generated by `tools/gen-permits.py` from the system
index and re-run by hand when Frontier locks or unlocks one.

Trade run is now its own Routing page, Trade route, rather than a card on Plan. Hops, most jumps
per leg, max distance from the star, max price age and every switch are now saved and survive a
restart; a voice plot given only the credits runs with whatever the page last saved. Credits to
trade with still asks every time and is never written to disk.

A new Trading Mode callout, off by default, says what to buy where you are standing for the system
at the end of your plotted route. It names up to two commodities, the station at the destination
that pays, the profit a tonne and the total for the hold, or says that nothing there sells at a
profit. It uses the filters saved on the Trade route page, stays quiet below a hold you set in
tonnes, and speaks once for each pair of station and destination. Your credit balance is still
never read, so the ranking is what you could carry rather than what you could afford.

"Best commodities to buy for Sol" now asks the same question Trading Mode answers on its own,
about any system: what to buy at the station you are docked at to sell there. It needs the market
you are docked at, uses the same saved trade filters and cargo default, and speaks the same
sentence Trading Mode does.

A career goal now names your rank — Harmless, Mostly Penniless, Trailblazer and the rest — rather
than reading it as a number out of eight. Reaching Elite no longer finishes the goal: it now runs
on to Elite V, the ceiling the game itself added, and reports each of those five grades by name.

The Adventures tab's two buttons keep their own height now instead of stretching to match the
description text beside them, which grew taller as panes opened and the description wrapped.

Every theme now computes its colours from that theme's accent and background, rather than storing
borders, fills and status colours as separate fixed colours or drawing accent tints at partial
transparency. On Elite and Guardian, body text is now the same colour as the theme's accent; Dark
and Light keep their existing neutral text colour. Every border, fill, rule and surface colour is
fully opaque, so a fill drawn inside a bordered card no longer comes out lighter than the same fill
drawn on its own.

Each row under "What leaves this machine" in Privacy and egress now opens at two lines — where it
goes, and one sentence of what — with the full paragraph behind a Show more press instead of drawn
open by default. What Ask says when asked directly is unchanged.

The stepper's arrows, the amount control's up/down buttons, and the three reset icons now take a
control theme of their own instead of drawing Fluent's gray hover and press states. At rest a
glyph shows only its mark; hover lifts the mark to full ink with no fill; a press fills Accent
with the bloom and sets the mark to Knock ink; disabled, or at a limit, the mark sits faint with no
hover or press. The card and placement-group reset icons now sit in a fixed 44×44 cell, reserved
whether or not there is anything to reset, so the heading's row no longer changes height when one
appears.

Hovering a button, a glyph button, an unselected tab or an unselected segment now shows a faint
Accent halo, and keyboard focus shows the same halo behind the focus ring, its 2px outline drawn at
full strength as before. A settings row, a list row and a transcript message still show no halo on
hover. Light still draws none of it.

## 0.168.0 — Controls take the HUD dress

Buttons, toggle switches and the transcript tabs now draw d47's own look rather than the
Fluent default: a square rule border, an amber fill, and a squared, skewed tab rather than a
rounded one. The panel itself now carries a 1px rule frame with two corners cut on the
diagonal, on the desktop window and in the headset alike. Labels and tabs render in Saira Semi
Condensed and prose in Saira, both embedded rather than drawn from whatever font Windows
happens to have installed; the Elite theme's accent moves to a slightly lighter orange and its
info colour to a brighter cyan.

## 0.167.1 — Give the vertical scrollbar its own column

Every scrollbar in the desktop window, its dialogs and the VR panel drew over the right edge of
the content it scrolled, wider still once the pointer moved onto it. The content now stops at its
own column, and the bar shows at its full width whenever it is there — never wider, never
overlapping.

## 0.167.0 — Ask which engineers want a thing

One tool now answers across every engineer at once: who refers them, what earns the invitation,
what it asks for, and whether each of those is met. Asking "which engineers want sensor
fragments" now names both Chloe Sedesi and Professor Palin, rather than whichever one a single
lookup happened to find. Material lookups ("found at", "found in") no longer mix an engineer's
tribute into where a material is actually sourced — that question now belongs to the engineer
tool alone.

## 0.166.0 — Say what is left for an engineer

Asking "what is left for" an engineer, by name, now answers aloud with the prerequisites that are
not yet met — a referral, an invitation task, a tribute — each with d47's reading of it, such as
"120 of 200 handed over". Routed with no model turn, for every engineer in the directory. An
already-unlocked engineer says so.

## 0.165.2 — Custom checklist lines get an aligned checkbox

A custom line's tick is now a checkbox labelled "completed" instead of a switch, and sits beside
the reorder, Edit and Delete controls rather than above them. All five now share one vertical
centre on the card, whether or not the line is selected.

## 0.165.1 — Panel controls become dropdowns

The Checklist bar's scope button and the Ask for an adventure form's Reach and Length buttons are
now dropdowns. Picking a value no longer opens a chooser that takes the whole panel.

## 0.165.0 — Remove project ordering and checklist import/export

The Checklist tab's control bar no longer offers Order or Import/Export. Projects now sort
here-and-now first, then in the order they first appear on the list, with no way to set an order
of your own between them. A checklist can no longer be moved to another machine by file. An older
checklist file with a stored project order still opens; the field is ignored.

## 0.164.0 — Answer questions from the commander's career statistics

D47 can now answer from the journal's `Statistics` event — bank balance, combat, exploration,
mining, trading, crafting, exobiology and the rest of the sixteen sections. Ask for one section by
name, or for all of them, and each figure comes back under a readable name and in its unit —
credits, light years, or hours and minutes — dated to when the game last reported them.

## 0.163.0 — Delete completed checklist items in bulk

The Checklist tab's control bar now has a "Delete completed items" button, beside Import/Export.
It removes every Done line from the whole checklist — the Commander's own and every plan's — in
one confirmed press, ignoring the current filter and search. It is disabled when nothing is Done.

## 0.162.0 — Remove checklist tombstones

A plan revision that drops a line now deletes it outright, rather than leaving it on the list as
a muted "dropped by a later version of a plan" record. A line a later revision wants again comes
back as a new open line rather than one that remembers what it had earned.

## 0.161.0 — Focus the checklist on one engineer

The checklist filter now offers an entry for each engineer with unlock prerequisites on the
list, under its own "Unlocking an engineer" heading. Choosing one shows only that engineer's
invitation, tribute and referral lines — the same set "Add to checklist" put there — and the
filter falls back to Everything once the last of them is gone.

## 0.160.0 — Draw progress bars on engineer prerequisites

Each unlock prerequisite with a number behind it — a contribution total, a reputation reading, a
statistic, a rank — now draws a progress bar under its text, on the engineer detail page, the
Route page and the checklist item alike. A ceiling test (Uma Laszlo's reputation cap) fills fuller
the further under the ceiling the reading sits; a met line draws full; a line with no number draws
no bar.

## 0.159.0 — Add engineer prerequisites to the checklist

An engineer's Unlock Prerequisites now carry an **Add to checklist** control, on the detail page
and beside each engineer on the Route page, absent once every line is met. One press adds every
unmet line as a checklist item that tracks itself from the journal — a referral, the invitation
and the tribute alike — and never adds a line twice.

## 0.158.0 — Decide Hero Ferrari's meeting test from low conflict zone wins

Hero Ferrari's "Complete 10 surface conflict zones" requirement now reads as a floor on
`Combat.ConflictZone_Low_Wins`, the on-foot ground fight count, rather than standing undecided.

## 0.157.0 — Correct three Odyssey bartender sale counts

Kit Fowler, Yarden Bond and Wellington Beck's engineer prerequisites named the pre-4.0.18.08
sale counts. They now read 5 Opinion Polls, 5 Smear Campaign Plans and a total of 15
entertainment items, on the engineer detail page and the Route page alike.

## 0.156.0 — Drop route promotion, copy way-in systems

Every stop under an engineer's "The way in" now carries a copy glyph beside its system name, the
same control the engineer's own header shows. The "Put the route on my checklist" and "Put this
route on my checklist" buttons are gone from the Engineers tab, and the underlying tool no longer
answers "put that route on my checklist" or "promote this unlock". Adding individual unlock
prerequisites to the checklist replaces it.

## 0.155.0 — Merge Large cards into Hull pictures

Fleet › Ships had two switches for hull artwork and neither did what its name said. There is now
one: **Hull pictures**. On, cards carry their drawing, a ship's page shows the large picture and
plays the turntable, and missing files are downloaded. Off, fleet cards are text only, a ship's
page shows no picture and plays no turntable — even for a hull whose files are already on disk —
and nothing is fetched. Files already downloaded stay on disk; the setting only stops them being
shown. Flipping it redraws an open Ships page without a restart.

## 0.154.0 — Split the power bar by priority group

The Power gauge now splits into one span per priority group — the numbers Elite switches modules
off in, in order, when the plant cannot meet the draw — instead of one flat fill, with each
group's cumulative deployed draw labelled underneath. The bar also marks the 20%, 40% and 50% thresholds where a
damaged or destroyed plant's output falls. A fitted module's group shows read-only in its detail
pane; a planned module that still needs work gets a stepper for its group, 1 to 5. A ship boarded
before this shipped shows one flat bar and a note to board it again.

## 0.153.0 — Show planned megawatts on each slot row

A ship's slot rows now carry a megawatts figure: what the planned module draws with its blueprint
and experimental applied, or what is fitted where nothing is planned. A modelled figure — one d47
worked out rather than read off the game — carries the same `~` prefix and muted hue as the Power
gauge's own modelled reading. A slot too vague to cost, or a module that draws nothing, shows no
figure.

## 0.152.0 — Rename the active ship's badge, drop its redundant line

The active ship's card badge now reads "CURRENT SHIP" instead of "FLYING NOW". The line under its
name no longer repeats that it is being flown; it now shows the system alone, or the system with
its planned slot count, matching every other ship's card.

## 0.151.0 — Outline the slot the detail pane shows

The left-hand list on a ship's page now outlines the slot row whose detail is open in the right
pane, the same accent-coloured outline the fleet cards and the Engineers list already carry.
Pressing a different slot moves the outline to it; the on-foot item page outlines its open row the
same way.

## 0.150.0 — Name the slot atop its detail pane

The slot detail pane now opens with the slot's own name — "Large Hardpoint 1", "Utility Mount 1",
"Compartment 3 (size 5)" — above the existing "Fitted" and "Planned" headings, on ships and on
foot. Previously the pane opened with "Fitted" alone and said nothing about which slot it was.

## 0.149.0 — Remove fulfilled checklist items when set

A new Checklist setting, off by default, removes a derived item from the list once it is done
instead of leaving it ticked, and deletes a ship slot's plan once it is fully met — the tick and
the plan column both disappear, and the slot's page offers "Plan this slot" again. The "is done"
callout still speaks once before the item goes. Lines a commander writes by hand are never
affected, on or off.

## 0.148.0 — Adopt keeps derived checklist items derived

A checklist line written before a commander was known — before the first journal, or while
Elite was not running — now keeps its kind, key, source and hull when it is handed to the
commander who appears. Previously every such line arrived as an authored note, so d47 stopped
checking it against the journal and drew a switch beside it instead of a live verdict.

## 0.147.0 — The rescan sentence counts this commander's ships

"Rescan my journals" now says how many ships it found for the commander currently flying, matching
the "What is fitted, remembered" row above it. Previously it summed every commander the journal
folder had ever seen. Where the folder holds more than one commander, a second clause says how many
others there are and how many ships they have.

## 0.146.0 — A commander reset forgets the ships that came before it

Resetting a commander now clears the ships d47 remembers for that Frontier ID, both while it is
running and after a rescan. Previously a reset left every ship the commander had ever flown in the
"What is fitted, remembered" count and the Fleet tab, including ships from before the reset.

## 0.145.0 — A tab's own settings fit a narrow column

"Settings for this page" on the Fleet, Routing and Adventures tabs fits the width of its column, so
it no longer shows a sideways scrollbar when the column is narrow.

## 0.144.0 — The selected ship scrolls into view

Selecting a ship on the Fleet tab scrolls the ship list so its card is fully visible, when the list
is too long to show every ship at once.

## 0.143.0 — The Transcript copy button keeps its glyph

Clicking Copy on the Transcript tab and waiting past the "Copied" state now brings back the
clipboard glyph, not the words "Copy All".

## 0.142.0 — The privacy notice covers a downloaded copy

The donation privacy notice and the retention policy now state that a donation may be copied to the
holder's machine to be read, that the copy is one file per donation in one folder, and that it is
deleted once the object it came from has left the store. Nothing about what d47 sends, or when it
asks, has changed.

## 0.141.0 — Where to farm engineering materials

`find_material` now leads with two farming tiers ahead of the origins text: the general methods
that yield a material's kind, and the fastest hand-picked site for the top grade of its trade
group, with the trade that turns it into the grade actually asked about. A new
`get_material_farming_route` tool lists those sites in travel order from the Commander's own
position, what to collect at each, and the trades that follow — optionally narrowed to Raw,
Manufactured or Encoded.

## 0.140.0 — Ship name and wake words moved to their own capabilities

The settings page no longer mixes Persona and Listening rows under one "Its name" card: ship name
and Keep Ship AI name now sit in the Persona place, and the wake-word and corrections rows sit in
the Voice Input place, next to the rest of what Listening owns.

## 0.139.0 — Cost details by provider

The cost details window can now be asked the other way round: a picker under Running totals reads
one provider — ElevenLabs, Anthropic, whichever has been charged — down the same four windows,
models rolled up under that provider's figure and named in full, with no cap on how many are
listed.

## 0.138.0 — Engineers on the slot page

A ship slot with a plan now lists who can roll it, under the Planned block: Unlocked engineers
first, then Locked, each ordered nearest first with a copy glyph on the system. An unlocked
engineer ranked below the planned grade still shows, holding what they hold. On foot, a
modification slot lists the same way; a grade slot carries no engineer list. Before
`EngineerProgress` has been read, the list shows once, ungrouped, saying so.

## 0.137.0 — Neutron jumps give their distance

Each waypoint on a plotted route now says how far the leg to it is, alongside the jump count and
the distance left — on the Route tab's plan result, the mini panel, and the spoken answer. A plan
saved before this change shows no figure until it is plotted again.

## 0.136.0 — The resize cursor points the way it drags

In resize mode, the ray's cursor turns from a ring into a double-headed arrow over a resize
handle, pointing the direction that edge or corner drags. Held during a drag even if the ray
wanders off the handle it started on.

## 0.135.0 — Zoom and resize the headset panel from the keyboard

Add four system-wide hotkeys for the headset panel — zoom in (`Ctrl+Alt+=`), zoom out
(`Ctrl+Alt+-`), reset zoom (`Ctrl+Alt+D0`) and toggle resize mode (`Ctrl+Alt+S`) — reachable
while Elite has the foreground, the same as the overlay's own two keys. Entering and leaving
resize mode is silent; asking for it with the motion controllers off or no headset session is
spoken aloud, since a Commander in the headset has no transcript to read it from.

Resize mode also opens and closes from a ray. A glyph beside the panel's help mark enters it on
the full panel, when the motion controllers are on; once the handles are up, a bar along the
bottom offers zoom out, reset, zoom in and done. The mini panel carries the bar too, with no entry
glyph — voice and the hotkey are what open resize mode there.

## 0.134.0 — Reset the headset panel's position by voice

Add "reset the panel", which forgets where the on-screen headset panel was placed and puts it back
where a fresh install puts it: world-locked, at its default distance, resting in front of you on
the next active tick. Works with no headset session attached and no model configured. Each
placement heading in Settings — the panel you are looking at, the full panel, the mini panel —
carries the same reset as a glyph.

## 0.133.0 — The headset panel goes where you look

Add "place the panel here", which moves whichever headset panel is on screen to the middle of the
direction the headset is facing, as far away as it already was, turned to face you. Looking down
places it lower and looking up places it higher. The panel is left world-locked, its place survives
a restart, and no model is needed.

## 0.132.7 — Kokoro's rows hide unless a voice actually uses it

Fix the "Local voice" and "Local voice model build" rows in Settings, which showed beside every
other provider's rows once Kokoro was downloaded, whatever provider each voice slot actually used.
They now follow the same rule as a provider's key row: on screen while some slot — the ship's own
or any other — names Kokoro, and hidden otherwise.

## 0.132.6 — Microphone and speech recognition settings share one card

Merge the Microphone and Speech recognition settings places into one card, "Voice Input", holding
both as named groups. Both carried the same help topic already; there is no longer a reason to
look in two places for one topic.

## 0.132.5 — Ship names and common words draw no chip

Fix system-name chips drawing on a ship or module name that happens to contain one — "Caspian
Explorer" no longer draws a Caspian chip, and "Rail Gun" no longer draws a Gun chip — and on a
one-word table hit that is an ordinary English word such as Arm, Long or Union. A Commander
actually in one of those systems still gets the chip.

## 0.132.4 — What to say comes from the phrase book, not a guess

Add `find_phrase`, so asked what to say for a goal the model reads the model-free router's own
phrase book instead of guessing or denying a working phrase exists. Two phrases that reach the
same thing are named together, and a goal nothing matches gets told so plainly rather than being
told the goal cannot be done.

## 0.132.3 — No invented chatter where nobody lives

Fix Passersby and Hail chatter firing in systems with population zero: they invented other people
nearby, or someone hailing the Commander, a thousand light years from the bubble with nobody around
to have said it. Controller chatter is unaffected — it only ever fires docked, where a station or
the Commander's own carrier already justifies the scene.

## 0.132.2 — The Power gauge counts the cargo hatch and the hangars

Fix the Power gauge undercounting a build whenever `ModulesInfo.json` has not been read for that
ship: the cargo hatch draws power from no slot in the outfitting layout, so it was never visited,
and the Mk II vessel hangars and free fighter hangars had no figures in the table at all. Both now
count.

## 0.132.1 — The Fleet page states the ship's real figures

Fix the Fleet page, `list_ships`, and the `ShipLoadout.MaxJumpRange` doc comment describing the
game's `MaxJumpRange` as a full-tank figure. It is not: it is unladen mass plus one jump's fuel,
the same figure `JumpGauge.Best` already reports. A full tank or a laden hold both jump shorter
than the number shown.

## 0.132.0 — The route panel no longer takes callouts down with it

Fix a crash on the tick thread when a route was plotted while the Routing tab's mini panel was
live: it dropped that tick's journal events, including the arrival that ends a long-jump
countdown, so the long-jump callout fired at the full duration on jumps that landed early.

## 0.131.0 — Crew on the intercom, not just the line

Address a hired pilot by name and they now keep the line the way the carrier's captain does:
follow-up questions reach them with no name needed until you say "that's all" and its kin, name
your ship AI, or name a different pilot. Each pilot keeps their own conversation rather than
sharing the ship AI's, and the ship AI hears each exchange as one it overheard rather than one it
had. A pilot is never offered a tool.

## 0.130.0 — The carrier captain on the line

Ask "carrier report", "carrier status", "how is my carrier", "carrier services" or "carrier fuel"
for the fleet carrier's fuel, cargo against capacity, balance, jump range, docking access, booked
jump and every service with who staffs it — apart from "where is my carrier", which still answers
with the system alone.

Start a question with "Captain" and the captain of your fleet carrier answers it, in the captain's
voice, from the carrier's figures and the galaxy tools. Follow-up questions go to the captain without
the name until you say "that's all", "that'll be all", "thank you captain", "dismissed" or "carry on",
or start a question with your ship AI's name. The ship AI hears those exchanges and can refer to them
afterwards. With galaxy search on, a carrier more than 500 light years away is out of range: the ship
AI tells you so and gives the distance. Beyond 250 light years the captain's line weakens as the
distance grows: more static, a narrower voice, short dropouts, and lost words marked with an
ellipsis in the panel and the caption. The ship AI hears only the words that came through.

## 0.129.1 — One tool surface for every provider

On Claude Opus 5, Opus 4.8, Opus 4.7, Fable 5, Mythos 5 and Haiku 4.5, through Anthropic's own
endpoint, the model is now given every tool it may use and searches them for the ones a request
needs, instead of receiving only the tools for the mode you are in. The list no longer changes when
you board the SRV, step out on foot or turn key presses off, so a mode change no longer re-bills the
cached prompt. A control that does nothing in your current mode is still refused, with the reason,
when the model calls it. Claude Sonnet 5, the OpenAI providers and custom Anthropic endpoints keep
the mode's list. If Anthropic refuses tool search for a model, the request is sent again with the
mode's list, and later turns on that model use the mode's list for the rest of the session.

## 0.129.0 — Settings one area at a time

Every two-state control in the app is now drawn as a switch — the checklist's Goals and Include
Partial Grades toggles, the engineer directory's two filters and a pinned blueprint, the route
planners' loop and pad checkboxes, the market page's three filters, the logbook's date range, Help
Improve's two toggles, the key editor's reveal, an adventure's "This ship only", and the two
keyboard swaps — in place of a ticked square or a button that changed colour to show a state.

The Settings page shows one area at a time: the nav lists every area, but only the selected one's
sections, and the page opens on that area's own title and sentence above its cards. Following a help
card into a setting, or reopening the page on a setting you were last reading, switches to its area
first. A search still draws matches from every area at once, each behind its own area's name, and
clearing it goes back to the area you had open. Below the width the nav collapses at, a dropdown of
area titles takes its place.

A section whose rows are all hidden behind "Show every setting" now keeps its card, with a "Show N
more" button in place of its rows; pressing it draws that section's own hidden rows without opening
any other section's. Following a help card into a setting still unfolds only the section it lands on.

A search now also matches an area's name, a section's name or one of its search terms (Microphone
answers to "mic" and "ptt", Its voice to "tts", Speech recognition to "stt" and "whisper", Privacy and
egress to "telemetry" and "data"), and a named group's title or help — each shows every row under it,
marked the same way a row's own words are. A row that lives on a tab rather than the Settings page —
Fleet › Ships, Routing › Community Goal, Adventures, or the Checklist tab's own row — now shows up
under "On other tabs" with a button that opens the tab and root it belongs to, its own strip open
where it has one.

Motion controllers default on for a new install — a settings file that already exists keeps
whatever it held, so an existing install turns them on with "motion controllers on". The Motion
controllers row no longer carries a warning pill or sentence.

A line spoken within thirty seconds of the last time the Commander was addressed by name — from any
voice, ship AI, callout, carrier captain or tower — now drops "Commander" from what is said aloud.
Captions and the flight recorder still show it; only the audio changes.

A callout, carrier line, canned message or story beat with an in-character rewording is now put to
the model only half the time by default; the rest of the time it is spoken exactly as written, with
no model call. Ambient remarks are unaffected and are still reworded every time.

Eight optional Guardian voice treatments for the ship AI — Cylon, pitch down, an octave-down layer,
chorus, metallic resonance, ring modulation, glitch and reverb — are now switches under Its voice,
all off by default and global to every core. Turning any on reaches the ship AI's turn replies, its
own callouts and a persona's introduction or return; the crew, the carrier and every over-the-air
voice are unchanged.

A Test button under Guardian voice plays a line through whichever of the eight treatments above are
on, and never bills a provider: it plays free where the provider is free, a free sample where the
provider has one, an audition already paid for this session where there is one, or a bundled
stand-in voice.

## 0.128.0 — Settings arranged by area

Fleet › Ships, Routing › Community Goal and Adventures now carry a "Settings for this page" strip of
their own — Rescan my journals and Hull pictures on Ships; the Inara API key and the week's turn on
Community Goal; Notable places for adventures on Adventures. Closed by default, and remembered open or
closed on its own account. Those rows are no longer on the Settings page.

The Settings page, in the window and in the headset, is arranged in six areas: Voice and hearing, The
ship's AI, Speaking up, Acting on the game, Screens, and Privacy and this install. The nav lists each
area with its sections beneath it, and a section gathers the rows for one job from wherever they were
declared, so When a turn fails is under The ship's AI and its reset puts back only its own rows. Which
sections you had closed, and where the page was left, start again from the defaults, except for
Persona, Callouts, Privacy and egress, and Diagnostics.

## 0.127.0 — Open the new plan after plotting

Plotting the Neutron Plotter, Road to Riches or a trade run now opens that plan's result page rather
than leaving the Commander to press "Show most recent" themselves. A second plot while its result page
is already open — from either surface's card, or by voice — updates the same page and its breadcrumb in
place with the new plan, rather than showing a stale one under a stale heading. A plot that finds
nothing to record leaves the surface on the form, as before.

## 0.126.0 — Surveyed biology from Spansh

Arriving in a system now gets a spoken callout naming the bodies Spansh has surveyed biology on, when
their species together reach the biology threshold: "Spansh has surveyed biology here: 3 b at 15
million, 3 c at 12 million." It needs galaxy search, which is off by default, and while it is on each
jump sends the system's address to spansh.co.uk; the Privacy and egress section says so. A body it
names is not called out again when you scan it. On by default, with its own toggle beside the other
callouts.

## 0.125.0 — What a body could hold

Scanning a landable body the FSS has counted biological signals on now gets a spoken callout when its
biology could reach 10 million credits or more: "3 b could hold up to 18.2 million in biology: Stratum,
Bacterium." The figure is the best case from the species the body's conditions admit, one per genus;
once a surface scan names the genera, the line gives a range over those. Said once per body. On by
default, with its own toggle and threshold beside the other callouts.

Asking what is on a body now says the same estimate. An FSS-only body names the possible genera and a
best case; a surface-scanned body gives a low–high range over the genera it actually found. Either way,
the exact value still waits on a sample — the genus is all Elite ever names before then.

Both now drop a species that cannot occur in the Commander's own galactic region, so a body near the
Scutum-Centaurus Arm no longer gets credited with a variant that only pays out elsewhere.

## 0.124.0 — Supercharge

Stepping onto a body for the first time now gets a spoken callout: "First footfall on Smojue
EB-O d6-37 AB 1 b." Directive 47 infers it from the body's scan and the `Disembark` that follows,
since Elite writes no event for a first footfall. On by default, with its own toggle beside the
other callouts.

Arriving at a system nobody has sold data on now gets a spoken callout: "Undiscovered system.
Nobody has sold data on this star yet." It fires on the arrival star's own autoscan, so a
`NavBeaconDetail` scan carrying the same flag in a populated system stays silent. On by default,
with its own toggle beside the other callouts.

"Plot next neutron jump", "plot next riches stop" and "plot next trade stop" plot the next stop on
a stored plan through the galaxy map, by voice, with no model needed. The next stop is the first
one after the furthest reached whose system is not the one the Commander is already in, so a
trade plan's first stop — the station it was plotted from — is never plotted back to. The answer
names where the stop sits on the plan, and a trade stop's answer also names the station.

A stored plan's result page now ticks off the stops the Commander has reached, muted, with the
row after them marked as the next one to copy. The page redraws as the reached stop moves,
without needing to be reopened, on the desktop window and in the headset. This applies to all
three plan kinds; on Road to Riches and trade plans, a tick means the Commander got there, not
that the bodies are mapped or the cargo is traded.

Mini's Routing tab now shows the last neutron route plotted, as a list of waypoints with the next
one marked, instead of the three planner forms. The next waypoint comes from `NavRoute.json` where
it names one, otherwise from the Commander's current system; where neither matches, the list is
still drawn with nothing marked. The full panel is unchanged, and the other three Routing roots
keep drawing their full-size pages in mini.

A route warning no longer fires on a leg that a neutron star or white dwarf supercharge would
clear. The strand check now measures the jump beyond an unscoopable star against the boosted
range for the fitted frame shift drive — six times normal range at a neutron star and three at a
white dwarf for the Mk II overcharge booster, four and one and a half for any other hyperdrive.
Where the boost clears the leg, the urgent warning is replaced by a quiet line naming the
supercharge; where it does not, the warning still fires, quoting the boosted range.

## 0.122.0 — How do I

A window left at full height comes back at full height. Restoring a remembered size used to
apply the same 90% margin as a fresh window's opening size, so a window snapped to fill the
screen reopened noticeably short of the taskbar; the margin now applies only when there is
nothing remembered.

The desktop window no longer shrinks to mini. It was always the full panel now: the setting, the
`Ctrl+M` hotkey, the spoken phrases and the shrink mark are gone. The headset's own mini panel and
the flat overlay are unchanged.

"How do I plot a course", "how can I get my ship engineered", "how would I turn off silent
running" — these now land on the one feature they mean, without the model. Where the words fit
two or three features equally well, d47 asks which; where they fit none, a configured model
answers instead, and with none configured this falls back to the top of the spoken map rather
than saying it has no way to work it out.

A wording d47 has learned now has a page: Settings → Learned phrases lists every one, newest
first, said next to the phrase it runs. Press Forget, or say "forget 'set focus on elite'", and it
stops matching — it goes back to being offered as a near miss rather than run silently.

With no model configured, an utterance nothing else can answer no longer just says it has no way
to work it out. If it shares words with a feature, d47 names that area and offers its list; asking
for the list gives the same drill "what can you do" would. With a model configured, an unmatched
utterance still reaches it as before.

Settings → About's Version row now has a Check for updates button, so a release that comes out
while d47 is running can be found without restarting. It works whether or not the startup check is
on, and says what it found: an update, that this build is already the latest, that GitHub could not
be reached, or that a local build has no release to compare against. Finding one adds an Install
row beside it, doing what Update now on the home tab already does — either button reaches the same
install, so only one runs at a time.

## 0.121.0 — Did you mean, and what can you do

A command missed by a word is no longer handed to the language model to refuse. Say "set focus on
elite" and d47 asks "Did you mean 'set focus to elite'?"; say "yes" and it does it. Where only one
harmless command fits once "on" and "to" are read as the same word, d47 just runs it. Anything that
presses a key in Elite is always asked first, and anything other than a yes or a pick drops the
question.

"What can you do" is now a drill rather than one long answer: d47 says how many areas there are and
names them, then asks which one. Answer with an ordinal, a name, or enough of one, and it goes a
level deeper, down to one feature — its own sentence, a phrase or two that works, and the panel
page it lives on where it has one. Anything else drops the question and answers normally.

Say "yes" to a "did you mean" offer and, once it has run, d47 asks whether to remember your own
wording as another way to say it. Say "yes" again and it does — say it that way next time and it
runs with no offer at all. It never asks twice about the same wording in one session, and never
asks at all for a one-word utterance, one that is already a declared phrase, or one already
remembered for something else.

## 0.119.0 — Ask how to get it

A new question, "how do I get X", answers for anything named — a ship, module, suit, hand weapon,
modification, material, ship-locker item, or commodity — rather than only the three things the
model could already ask about separately. It says how the thing is acquired first, then names the
nearest shipyard, market, mining ring, or material trader where finding one is part of the answer.
A rare good and a tech broker unlock are answered from the table alone, with no search needed.

## 0.117.0 — Engineering, seen two new ways

A ship gated behind an Empire or Federation naval rank now says which rank it needs — the
Imperial Cutter asks for Duke, the Federal Corvette for Rear Admiral — and the Cobra Mk IV says it
is Horizons early-adoption only rather than reading as an ordinary shipyard buy.

The checklist can now be filtered to what a pinned blueprint can finish, wherever you are docked —
not only where the engineer's own workshop is. Pin a blueprint on an engineer's page in the
Engineers tab ("A blueprint is pinned with them"); the filter then keeps the lines that engineer
covers and your rank with them already clears, dropping anything that would still need a trip to
the workshop. It sits under its own heading in the Show chooser, and does not go away when you
leave the system an engineer works in — the point of a pin is that you do not have to be there.

An engineer's unlock prerequisites now read your rank, reputation, career statistics and
contributions where the game states one of those as the test, instead of only what your journal's
`EngineerProgress` says about them directly. Where the reading falls short, the criterion shows it
— "Last reported 4,558", "18 of 25 handed over", or a stale reputation reading's date — on both the
engineer's own page and the Route page, in the SteamVR overlay as well as the window.

## 0.116.0 — Voices that suit who is speaking

A station under Imperial control now speaks in a British-sounding voice, where your speech
provider offers one — read off `StationAllegiance` on the `Docked` event. Most stations name no
allegiance at all and are unaffected; Federation and Alliance stations keep the voice they always
did.

Listening to ElevenLabs voices in the voice list no longer costs money. The play button now plays
ElevenLabs' own free sample of each voice. A second button beside it has the voice say your core's
line, billed as before, and the price above the list now refers to that button. A voice with no
sample keeps the single paid button.

With personality on, the game's own NPC lines are now said in other words rather than word for
word each time. A trader's "I strongly advise you against this." is reworded in a trader's voice,
a pirate's in a pirate's, and station traffic stays a procedural notice; each keeps its attitude,
so a threat is still a threat. Only lines Elite itself wrote are reworded — a message another
player typed is still read exactly as sent — and the comms panel still shows Elite's wording.
With personality off, they are said as written.

Speech settings now has a "Reset every voice to its pairing" row. Pressing it puts every core back
to the voice d47's pairing pass chose for it, and the carrier captain and tower back to speaking in
the ship AI's — undoing any voice you have since hand-picked. It covers every voice provider you
have used, not only the one selected now, and a core with no recorded pairing is paired again
rather than left as it was. A first press only asks; a second press within a few seconds is the
confirmation.

## 0.115.0 — Resize the panel where it sits

Place mode (`Ctrl+Alt+M`) now offers a resize as well as a move. While the border is up, the
overlay strip takes drag handles on its top edge, its bottom edge and all four corners — a corner
drags both dimensions, an edge drags one. A drag stops at a minimum rather than going to nothing,
and the new size is remembered the way the position already is.

The headset panels resize too. Say "resize the panel" and the panel on screen shows a handle along
each edge; point a controller at an edge or a corner, hold the trigger and pull. The panel changes
shape in the room and its content reflows into the new space rather than being stretched. Say
"stop resizing" or press the grip to leave the mode. The mini panel's resolution is now a setting
like the full panel's, so mini describes how much is shown rather than a fixed size.

The panel's zoom can be stepped from inside the headset: "zoom the panel in", "zoom the panel out"
and "reset the panel zoom" change how large the content is drawn without moving the panel's edges.

## 0.114.0 — Timers and alarms behind a startup flag

Clocks, timers and alarms are off unless d47 is started with `--utilities` or with
`D47_UTILITIES=1` set, and the switch lasts for that run only. Without it there is no Utilities
tab on the desktop, in the headset or on the overlay strip, no timer or alarm tools, no "what time
is it" or "cancel the timer" phrases, and `alarms.json` is not read. d47 also does not know the
date in that case, and says so when asked rather than working one out.

## 0.113.0 — Copy any system name the panel shows

A system name could be copied from the Ships whereabouts line, the Community Goal table, or a
route being planned or flown — three different mechanisms, each reaching the clipboard its own
way, and none of the three reaching the Carrier, Sourcing, Market or Engineers pages at all. Every
system name the panel draws now carries the same copy glyph, wired through one seam, so it is
available everywhere a system name is shown — desktop and headset alike, since the shared control
never depends on the desktop window's own clipboard.

The Journal reading now draws the same glyph beside any line whose event names a system — an
`FSDJump`, a `Docked`, a `Location` — and none beside a line that names none.

The In Ship reading now draws a copy chip under any turn that names a system, one per name in the
order it was said, whichever side of the conversation said it and in the headset as well as on the
desktop.

A drag on the In Ship page that starts in one bubble and ends in another now keeps both ends and
every bubble between them, rather than only the bubble it started in. Copy puts the whole
selection on the clipboard, d47's lines and the Commander's own both included, in the order they
are shown.

## 0.112.9 — Controls that draw their state

The Engineers tab's Colonia filter was a button that rewrote its own label — "Hide the Colonia
eight" or "Show Colonia again" — so the only way to read which way it was set was to read the
offer to change it, and the setting was gone at the next launch.

It is now two checkboxes, **Hide the Colonia eight** and **Hide on-foot engineers**, both
unticked by default and both remembered between sessions. Either can take rows off the Directory
and the Route page's ranked list without touching how the ranking itself is worked out or what the
spoken answer says.

The expand and shrink marks on the mini panel's way out were four corner brackets at a size where
the arms were shorter than the pen was wide, so the mark read as a plus sign. Both are now diagonal
arrows — heads outward for expand, inward for shrink — drawn on a finer pen and at the size the
rest of the mark family uses.

The Ships index carried the one switch in the app that did not follow the settings page's own
pattern: a `ToggleSwitch` with its label as `Content`, which stacks the word above the knob and
wraps it onto a second line. It now reads **Large cards**, the label as a `TextBlock` beside a bare
switch with no `On`/`Off` text, on one line in both the desktop panel and the headset.

## 0.112.8 — An engineer's row says who it opens

An engineer whose referral gates somebody else drew no line about it, however much the Commander's
plans needed the other end. Marco Qwent's own page said nothing about the fact that reaching grade
3 with him is what opens Professor Palin, Lori Jameson and Chloe Sedesi.

An engineer not yet at the referral grade a plan-named dependant needs now says so, on the
directory row and the engineer's own page: "grade 3 opens Professor Palin, Lori Jameson and Chloe
Sedesi." An engineer whose referral is already met, or whose dependants no plan wants, says nothing
extra.

An engineer's page and the spoken engineer report no longer carry a "Reputation rises fastest by"
line. It stated the same mechanic for every engineer — crafting and selling raise it — and the base
above it already named the one activity that was specific to them.

An engineer's page named nowhere which of the Commander's plans it was the answer to, though the
directory counted them. It now lists the planned work that names them, before the cost of reaching
them — "Planned work" once unlocked, "What unlocking them buys" while they are not, capped at eight
with an "and N more" tail.

## 0.112.7 — Lore is remarked on once a week, not once a day

A Commander whose home system is where they spend most sessions heard the same lore remark every
day. The quiet period between remarks on the same system is now a week by default, and a row under
Lore in Settings — "How often a system's lore is worth repeating" — holds the number of days.

## 0.112.6 — A squadron acceptance is spoken, naming the squadron

A squadron accepting the Commander's application went unsaid, the same as any other journal event
d47 did not recognise. It is now a narrated line naming the squadron: "Application to GREYBEARD
DELTA approved."

## 0.112.5 — A confirmed plot says the jump count and the distance

A plotted course said only that it worked: "Course plotted to Colonia." The Commander still had to
open the map to find out whether that was four jumps or four hundred.

The confirmed sentence now carries both: "Course plotted to Colonia. 43 jumps, 22,000 light years."
The jump count is the jumps still ahead of the Commander, not the hops in the route file. The
distance is left out when any leg of the route has an unknown length. A route the Commander plots
by hand, or a plot that could not be confirmed, is unchanged.

## 0.112.4 — A ship with no Fuel Scoop is warned when its fuel will not finish the route

Flying without a Fuel Scoop silenced every route fuel warning, because each one was about a star
that could not be scooped. A Commander whose only way to refuel is a station heard nothing when the
route was longer than their fuel would last.

Fuel on a route is now its own warning. It works out how many jumps the fuel in the tank covers,
from the fuel each jump has actually used this session, and warns when the route is longer than
that with nowhere to refuel inside it: "Fuel warning. At 2.1 tonnes a jump there is fuel for 4
jumps, and the route has 9 jumps left." A ship with no scoop hears nothing about stars. A ship with
a scoop hears how far away the nearest scoopable star on the route is.

The line that said an unscoopable next star left too little fuel for the jump beyond it is replaced
by this warning. The warning about an unscoopable star before a jump that is out of range is
unchanged.

## 0.112.3 — A sold ship's build is deleted with its checklist lines

Elite reuses ship ids: sell a ship and the next one bought can take its number. A build pinned to
that number stayed, and its checklist lines were spoken, ranked and drawn as "That ship id now
reports a Panther Clipper Mk II, and this plan was written for a Sidewinder" — a plan for a ship
that no longer exists, with nothing to do about it.

A build is now deleted when its ship is sold, or when its ship id reports a different hull, which
catches sales made while d47 was closed. Every checklist line the build put on the list is deleted
with it, and nothing is said. The plan cannot be recovered.

Notes you wrote yourself on a ship's list are no longer deleted when the ship is sold, and the sale
is no longer announced as "I cleared 2 items from your list".

A rank gate on the checklist no longer teaches the engineering rank mechanic every time an item
becomes blocked. "Grade 5 cannot be crafted at rank 3 with Marco Qwent at all" is still said and
still shown; "Rank rises by working with them, and it compounds" no longer follows it, on the
twentieth unlock or the first. The Checklist page still shows a blocked item with its reason, and
asking still answers — only the unprompted callout is silenced.

## 0.112.2 — The search box shows up on a filterable tab's first visit

Opening Checklist, or another tab whose panes filter, from a cold start left the search box
missing until the tab was left and revisited. The row was decided from the tab's drill strip
before the strip had drawn its panes for the first time, so the question "does this tab filter"
was asked and answered "no" a layout pass too early, with nothing to ask it again once the strip
had finished drawing.

The strip now says when it has drawn, and the panel asks the question again when it does. The
search box is there on the first look.

## 0.110.49 — Turning off a chat or chatter switch now takes effect immediately

Seven rows in Speech, "Other voices" and the personality half of the two chatter gates were read
once, when d47 built its callouts at startup, and never read again. Turning "Include NPC chatter"
off, or any of the six channel switches next to it, changed nothing until the next launch. Turning
personality off silenced neither "In Ship chatter" nor invented "NPC chatter" without a restart.

All of them now read the live settings, so a switch in the panel is obeyed on the next tick.

## 0.110.48 — A version number is read aloud instead of throwing on ElevenLabs

Reading a version like 0.112.0 out loud threw, because the number reader split it on the first
full stop only and then read the rest, "112.0", against a digit table meant for single digits.
The sentence was dropped and a warning logged instead of being spoken.

Every dot-separated group after the first is now read digit by digit, the way a decimal fraction
already is: 0.112.0 is "zero point one one two point zero". Asking Directive 47 for its status
now gets an answer on ElevenLabs.

## 0.110.47 — A stick that is plugged in is no longer reported missing at startup

Windows takes a moment to report every game controller it has, and D47 waits for that list to stop
changing before reading it. The wait was not counted properly: the fifteen reads a bound stick gets
to turn up in were being spent on reads taken during the wait, which report nothing whatever is
plugged in. Whether D47 then warned that push-to-talk was bound to a controller that is not here
came down to which side of that moment the reads happened to fall.

The stick is now read only once the list has settled, so the fifteen reads are fifteen real chances.
A stick that is genuinely absent is still reported, and the push-to-talk key on the keyboard is read
on every tick as before.

## 0.110.46 — A SteamVR registration that keeps being refused says so once

D47 registers its controller bindings with SteamVR as soon as the headset session is up, and tries
again on every tick until SteamVR takes them. When SteamVR refused for a reason that did not clear,
that retry rewrote the six binding files on disk and wrote the same warning to the log ten times a
second for as long as the session lasted.

The retry is unchanged, because the refusal often does clear. The files are now written once, and each
distinct reason is logged once. A different refusal is logged once more, and the registration
finally going through still says that controller input is on.

## 0.110.45 — A part of D47 that keeps failing is paused rather than retried forever

Almost everything D47 does runs off one loop that ticks ten times a second. A part of that loop
which threw on every tick was called again on the next one regardless: one installed build threw
25,000 times in an hour, wrote 250 error lines, and ran the part of its work that came before the
throw ten times a second for the whole hour.

A part that fails ten times running now stops being called, and is tried again once a minute until
it succeeds. It resumes by itself when the fault clears, so a window that has since opened or a
file that has since appeared no longer needs a restart. One part failing on and off is untouched:
the ten have to be consecutive.

A paused part is a feature that is not running, so it is named rather than left to be inferred.
Ask **what's your status** and the report names it, the Diagnostics card carries a row naming it
while it is paused, and the log at shutdown says whether it was still paused when D47 closed.

## 0.110.44 — Jumps left is read from the plotted route

Asked how many jumps were left in a route, D47 answered with how many had been made this session.
It kept its own note of where the ship was heading, taken from the journal's `FSDTarget` event, and
treated arriving as spending that note. On a plotted route Elite targets the *next* hop about seven
seconds into the hyperspace tunnel, before it writes the arrival, so arriving threw away a target
the game had already set. The route line then said nothing at all while the Commander sat in a
system mid-route, and the only jump count in front of the model was the session's completed jumps.

The spoken answer and the Route Progress panel now read the same file, `NavRoute.json`, so the two
cannot disagree. Both name the next hop and the jumps left from where the Commander is standing;
both say the Commander is not on the route rather than counting the whole route as still ahead when
they have jumped off it; and neither mentions a route when none is plotted. The remark on a long
crossing names the system being flown to rather than the hop after it, and a system name read aloud
states the class of the star the Commander is actually sitting next to.

## 0.110.43 — Target the next system in the route by voice

Say **next system**, *target the next system* or *target the next system in route* and D47 targets
the next system on a plotted route, the way `TargetNextRouteSystem` does on its own binding. It
refuses rather than pressing a key with nothing to target: with no route plotted, or standing on
the last hop, it says so instead of acknowledging a key that would have done nothing.

## 0.110.42 — Ask D47 to request docking

D47 could take a ship out of a station and could not bring it in. Say **request docking** — or
*request permission to dock*, *permission to dock*, *ask for docking*, or *take us in* — and it
walks the left panel's contacts tab and asks the station for permission. Elite binds no action for
requesting docking, so this is a menu walk, and a blinder one than taking us out: the status file
says a panel is open and never which tab is showing or which row is selected. D47 therefore
confirms by the journal instead, and says whether a request actually went in rather than assuming
the walk worked. It refuses before pressing anything unless the ship is undocked, in normal space
and has a destination selected, and *take us in* refuses with no docking computer fitted, since
that phrase promises an approach D47 does not fly. It has its own settings row beside the other
compound commands.

## 0.110.41 — D47 follows the Default Device as it moves, not just where it started

Left on "system default", the microphone and the speaker resolved which device that was once, at
startup, and held it for the rest of the session. Switching on a headset mid-session moved
Windows' own default without d47 noticing, so it kept listening to and speaking through whatever
had been default before. Both now register for Windows' default-device notifications and re-open
once a move settles — one re-open for a burst of several, and only on whichever direction is left
on "system default"; a chosen device is unaffected. A line already playing finishes rather than
being cut off mid-word, unless the device playing it has itself disappeared, in which case it
stops rather than continuing on a device no longer there. A microphone move that lands
mid-sentence says it did not catch that rather than dropping the words silently.

## 0.110.40 — D47 now listens on and speaks to the same Windows default

The microphone and output device rows followed different Windows defaults: the microphone opened
the Communications device, the speaker opened whatever NAudio's own default happened to be, and
neither matched the Default Device a Commander sees first in Sound settings. Windows only splits
Communications off from the Default Device once a headset or voice-chat app is involved, which is
why the two roles agreed on some machines and named different devices on others. Both rows now
follow the Default Device, and the settings page names the device and which Windows row it came
from. **On an existing install left on "the system default", the
microphone d47 opens may change after this update** — check the Microphone row if a Commander
notices d47 has gone quiet or stopped hearing them.

## 0.110.39 — A journal history walk that did not finish is no longer reported as an absence

The walk over older journals ends one of three ways: it finishes, it throws, or it is stopped when
d47 quits. Only the first of those has read any history, but the carrier, ship list and loadout
answers treated a walk that threw as one that had finished, and said no carrier appears in any
journal read. They now say the history was not finished reading, the same as while it is still
running. Quitting while a walk was running also left it running, so it could still be writing the
file of heard names and logging after the log had been closed; quitting now stops the walk at the
next journal file and waits for it to stop.

## 0.110.38 — The window no longer waits for the journal history to be read

Four walks back through older journal files — the fleet, the loadouts, the carrier and the place
names — ran inside startup, before the window could be built. On a data folder that has already
recorded how far it has read, none of them takes noticeable time. On one that has not, which is
every first run of a fresh install, the names walk reads every journal in the folder: 13.7 seconds
on one measurement, and 26 seconds on the launch that prompted this. The walks now run once the
window is on screen, and what they recover is folded into the game state on the next tick, without
displacing anything the current session's journal has already said. While a walk is running the
panel shows "Reading journal history" with the seconds so far, and asking about the carrier, the
ship list or the fleet loadouts answers that the history has not been read yet rather than
reporting an absence it cannot yet know about. `get_app_status` names the walk and its state.

## 0.110.37 — Startup no longer waits for the speech model, and a press made while it loads is kept

Loading the speech model ran on whichever thread asked for it: the startup path, and the window's own
thread on every change to a listening setting. On the maintainer's machine `medium.en` took about 1.2
seconds, the largest single step in a 3.5-second start, and picking a different model or microphone in
Settings froze the window for the same time. The load now runs off that thread, so the window appears
without waiting for it and Settings stays responsive while it happens. A press made before the load
finished used to be captured and answered with "I have no speech model loaded to understand it"; it is
now held and transcribed once the model is loaded. The microphone indicator says "Loading model..." while
that is true, rather than reporting that it is ready.

## 0.110.36 — The published build no longer decompresses itself on every launch

The single-file publish compressed every assembly into the exe and decompressed all of them into
memory on each start, and none of them were precompiled, so every method Avalonia and d47 touched
before the window appeared was JIT-compiled cold. Publish now leaves the assemblies uncompressed
and ahead-of-time compiles them. The exe is larger on disk; nothing about the install, the update
payload or the update check changes.

## 0.110.35 — The log says where startup time goes

A launch measured on the maintainer's machine took 46 seconds, 35 of them inside two stretches that
logged nothing while they ran, so what was slow could only be guessed at from what each stretch ended
with. Every step of startup is now timed — the settings and stores, each of the four journal
backfills, the priming tick, the audio output, the transcriber, the echo canceller, the Elite window,
the bindings, the controllers, the capability registry, the speech model and the three settings
applies — and a step that took 250 milliseconds or more writes a line saying how long it took. The
first and last lines of a launch also state how long the process had already been running, so the
time before d47 had anywhere to write is measured rather than inferred.

## 0.110.34 — Closing SteamVR no longer crashes d47

Closing SteamVR while the headset overlays were up crashed d47, with nothing in its own log after
the line saying the session had ended. The ninety-hertz thread that places the aim ray was
inside an OpenVR call when the tick thread shut the session down, and the memory it was reading had
already been freed. Ending a session now waits for that call to finish, and every call after it
returns without reaching OpenVR at all.

## 0.110.33 — Every engineer prerequisite draws a checkbox, on the Route page as well as the detail page

The engineer detail page marked each unlock prerequisite with a character — `✓`, `·`, `?` — read out
by nothing. Those become a drawn box, checked, empty or dashed, coloured accent, muted or informational
so a met one, an unmet one and one nothing d47 can read decides are each visibly distinct, and named
for a screen reader. The Route page's five ranked engineers now list the same prerequisites for the
engineer the chain ends at, under the summary sentence and above the existing route lines.

## 0.110.32 — An engineer no longer appears as a prerequisite for themselves

The engineer detail page's **What it takes** section used to end with "Liz Ryder works for you",
stating an engineer working for you as one of the conditions of that engineer working for you, when
**Where you stand** above it already answers the same question with more detail. That line is gone,
and the section is retitled **Unlock Prerequisites**, which reads correctly whether the engineer is
unlocked or not.

## 0.110.31 — The Engineers summary counts the game's three states, not d47's reading of reach

The line at the top of the Directory, the Route page, and the spoken answer to "who should I unlock
next" used to read "X of 38 unlocked, Y within reach" and a clause about planned work waiting on
somebody not yet unlocked — d47's own judgement of what could be acted on today, which counted an
engineer who has never been mentioned in the journal the same as one already invited. It now counts
the three states Elite itself writes: "9 of 38 unlocked. 16 in progress. 13 not started." The waiting
clause is gone; with it, nothing distinguishes a plan blocked on an engineer from one blocked on
materials, which is a real loss and may come back as its own line later.

## 0.110.30 — The Engineers directory counts modules left to finish, not rolls already done

An engineer's count on the Engineers tab used to add up every planned blueprint and experimental
effect they could in principle roll, including ones already applied and ones on a module they
cannot actually grade — Selene Jean showed 63 planned things while her own work was already done,
eight of them "Heavy Duty" on Shield Boosters, which she cannot touch at all. The count is now
modules still to engineer that the engineer could finish: a slot with something already applied at
the planned grade contributes nothing, a blueprint name shared by several module types is matched
against the module actually fitted, and an engineer who can only apply an experimental effect
without also being able to roll an outstanding blueprint on the same module is not counted for it.
The row reads "1 module" or "43 modules" rather than "1 planned thing wants them".

## 0.110.29 — A number in a system name is read as part of the name, the same way on every voice

"HIP 3269" was read as "thirty-two sixty-nine" by the local voice and "three thousand two hundred and
sixty-nine" by ElevenLabs, while Edge Neural, OpenAI and Cartesia were sent the digits and said
whatever they chose. Numbers are now written out as words once, before any provider sees the line, so
every voice says the same thing: a run of four or more digits is read digit by digit — "three two six
nine" — and a run of three or fewer keeps the casual reading, so "Col 385" is still "three
eighty-five". A number carrying a unit, a decimal point or a grouping comma is a quantity rather than
a name, and is left as it was.

## 0.110.28 — A long system name survives two dropped letters; a short one stops out-ranking it

"Set course for Shinrarta Dezhra" heard as "Shinrata Desra" was offered "Ra" — a real system whose
name happens to sit inside the misheard one — while the Founders' World, three edits away against a
budget of two, never came up. A catalogue name under four letters now has to be the whole word rather
than merely appear inside what was said, and a long compound name is allowed one more edit than the
length-scaled budget would otherwise give it. Separately, a failed tool call's arguments are now in
the log line alongside it, since the call itself is unrecoverable once the turn has ended.

## 0.110.27 — A misheard commodity name is offered its nearest match, not just refused

Asking for "Leavian Brandy" got told there was no such commodity, twice, because commodity lookup
never used the near-match table that already helps with ships, engineers and modules. It now checks
a name that matches nothing against the cargo and rare-cargo ledgers and, if one is close, offers it
by name — "Did you mean Lavian Brandy?" — instead of asking to be repeated. A name near a ship
material rather than a commodity is left alone, since a material is answered by a different tool.

## 0.110.26 — Asking about a rare good names its one station and what is on offer there

"How much Lavian Brandy can I get at a time?" used to be answered from the ship's cargo capacity,
because nothing else was available to answer it with: a rare is sold at one station, and a search for
markets near you finds nothing. D47 now names that station and system from a table it ships — no web
access needed — and reads how much is on offer there from the market's own last report, given with
its age. That figure, not the hold, is what limits a visit, and it moves with the station's economic
state, so a boom can put far more on offer. Where there is no recent report, the station is still
named and the quantity is said to be unknown. Asking where to sell one still sweeps the radius, since
that is a question the market index can answer.

## 0.110.25 — Three more throttle positions, "military thrust" among them

D47 could only set the throttle to zero or full. Elite's own preset already binds 25%, 50% and
75% to the keyboard, so "military thrust" (also "military power", "throttle to seventy-five",
"seventy-five per cent") now presses `SetSpeed75`, and the same phrasing pattern covers 25% and
50%. Unbound, each refuses by name and presses nothing, the way every other action does.

## 0.110.24 — A command that worked is acknowledged rather than narrated

"Gear down" answered "Pressed L for the landing gear" — the sentence written for the log, read out
loud. A command that works now answers in two or three words, varying rather than settling on one:
"Aye.", "Done.", "Acknowledged.", "Aye, gear down." The key that fired is still in the log and in
the result the model reads, because which binding actually went in is the first question on any
"it did not do it" report. Refusals are unchanged — they still name the action, the reason and the
binding — and so is the reply to a command with nothing to do, which corrects you rather than
acknowledging you.

## 0.110.23 — A failed plot is heard as the tool wrote it, not retold by the model

`plot_course` and "set a course and take us out" report a plotting attempt in one of three exact
sentences: plotted, no route appeared, or the outcome could not be told. Asking a model to repeat
that back left room for it to add something that was not true — offering to check the spelling, for
instance, when the tool never said the name was wrong. Those sentences are now spoken as written and
end the turn there, the same as any other tool result marked to be relayed.

## 0.110.22 — Asking about a stored ship reads its modules instead of refusing

"How is the Panther Clipper outfitted" used to answer with cargo, jump range and the like, then
say there was no way to read out its module fit unless you were aboard it — even though every
module of every ship you have ever sat in is kept in `loadouts.json`, the same file the checklist
already reads. Naming a ship now answers from the loadout last seen for it, dated so a ship
refitted since is visibly stale, and a ship you own but have never boarded says so rather than
describing a different one. And asking with no ship named now answers about the one you are
flying from memory after a restart, before Elite has written a fresh `Loadout` for it — the same
gap #337 closed for engineering, one level down.

## 0.110.21 — A command's refusal and the next callout no longer run together

A key-binding refusal spoken in answer to a command could be followed straight away by an
unrelated ambient callout — a High Grade Emissions report, a jump-remaining line — with no
separation between them, so the two read as one continuous sentence about the same subject when
they were coincidence. A short silence now separates two spoken lines from different groups, but
only once the first one has actually finished; a line cut short by Silence, a dropped group or an
alert does not wait for anything.

## 0.110.20 — Asking a second time shuts the dock hands up as well as the first

Chatter was cut off when you opened the microphone, but not always when you opened it again. If the
previous turn ended without an answer, nothing was spoken and no sound cue played, so the loop was
still showing that turn's outcome when you asked again. Opening the microphone then let the loop
return to idle in the middle of shutting chatter off, and that let chatter straight back in for the
whole of the second question. The loop now moves before the audio does, so a follow-up silences the
exchange exactly as the first question did.

## 0.110.19 — Talking to Directive 47 stops the dock hands talking

Ask something while a dock hand is mid-sentence and the answer used to queue up behind the whole
exchange, arriving after you had moved on. Invented chatter is now kept apart from everything else
that gets spoken, and opening the microphone cuts the chatter line mid-word and drops the rest of
the exchange. It goes on the microphone opening rather than on the answer arriving, because
transcription and the model together take several seconds and a four-line exchange finishes inside
them. Chatter then stays shut off until the answer is done and the loop is quiet again, because the
next speaker in an exchange is written and voiced while the first is still talking and would
otherwise arrive a second after you cut them off. Nothing else is dropped with it: a message the
game sent you, a callout you switched on and the sound cue in front of the answer all keep their
place. And only you do this — an unprompted line of Directive 47's own still waits its turn.

## 0.110.18 — A refused tool call is not tried again in the same turn

A plot request refused by the online gate (#408) was retried by the model five times in seven
seconds, each retry speaking "Plotting the course to Kamitra" again and the model's separate
replies running together with no space between them. A tool call is now run once per turn for a
given name and arguments; a repeat is told plainly that it was already tried and answered the same
way, without running the tool or speaking its announcement again. Text spoken across two tool
rounds in one turn is now always separated by a space.

## 0.110.17 — The Quartermaster stops reaching for a ledger word for everything

A weekend of logs showed the Quartermaster's accounting vocabulary leaking into lines that had
nothing to do with cost — a TV show got "decent margins", a session greeting rotated through six
different synonyms for "ready" ("reconcile the ledger", "balance the ledger", "run the ledger" and
more). The lexicon is now scoped in the prompt to cost and worth, dropped elsewhere, and the
session greeting is told plainly not to dress up "ready" in a core's own words unless something
real is worth naming.

## 0.110.16 — Passers-by chatter stops running one script

Overheard exchanges between a dock hand and a courier were one script with the names changed: a
third of them opened on the Commander's ship blocking a pad and closed on "not my problem, I'm
just here for the run". The cast, the topic and the opening beat now rotate deterministically per
exchange, and the worn lines the logs measured are named off limits in the prompt itself.

## 0.110.15 — Invented chatter at your own carrier knows whose deck it is standing on

Parked on your own fleet carrier, overheard chatter already knew whom to cast as the tower
controller and the captain but told the scene nothing about what your owning the place means to
the people in it. Now the model is told how your own crew regard you — deference or an easy
grumble made to you, not surprise — and how a visiting pilot at somebody else's carrier can react:
surprised, careful, or embarrassed to have been overheard. It colours the scene rather than
narrating it: ownership is the subject of at most one exchange a visit, the rest may only show it,
and none of this fires until you are actually on the carrier's deck.

## 0.110.14 — A System Authority vessel near your own carrier gets the owner treatment reliably

A System Authority vessel's canned line, heard while the Commander shared a system with their own
fleet carrier, was sometimes read out as an ordinary stranger's message instead of the reworded,
deferential line that setup calls for. The check for "does the Commander own a carrier here" was
installed as a side effect of an unrelated periodic pass over game state, so a line arriving before
that pass had run was always judged as "no" — including every line spoken in the first seconds
after Directive 47 starts. The check now reads live game state directly from where the callout is
built, so it is never standing in for "nothing has told me yet".

## 0.110.13 — NPC chatter stops inventing a dock in supercruise and normal space

Overheard chatter talked about landing pads and dock queues while the Commander was crossing a
system in supercruise, or flying in normal space nowhere near a station — an invented exchange
about a ship sitting on a pad it was nowhere near. Two fixes: no chatter at all in supercruise or
hyperspace, where nobody is near a ship to overhear; and the model is now told, in words, whether
the ship is docked or in normal space, with a normal-space scene for the passers-by and hail
pairings that has no pads, dock hands or queues to draw on. Docked scenes are unchanged, and a
controller exchange asked for while docked is dropped rather than composed if the Commander lifts
off before it is written.

## 0.110.12 — "Which of my ships has the best jump range" is answered rather than refused

Asked which of their ships with at least 24 tonnes of cargo space had the best jump range,
Directive 47 said it could only see the loadout of the ship being flown. That was never true:
every `Loadout` Elite writes is kept in `data\loadouts.json`, and cargo capacity and maximum jump
range are both in it. What was missing was a tool that could read it. There is one now. It lists
every ship as it was last seen fitted — cargo, jump range, unladen mass, fuel, value and rebuy —
and can narrow to ships above a hold size and rank them by jump range or cargo, so the comparison
is arithmetic on figures the game reported rather than a guess from hull specifications.

Two things the answer always states, because both are true and neither is a reason to refuse: the
jump range is the maximum on a full tank with an empty hold, which is the right figure for ranking
ships against each other and the wrong one for a laden run; and every ship carries the date it was
last seen, because one refitted since you last boarded it is remembered as it was. A ship you own
that no `Loadout` has been read for is named rather than left out, so a ranking is not mistaken
for the whole fleet.

## 0.110.11 — "Which engineer is in this system" finds them, instead of a lore lookup's empty answer

Asked who the engineer was in Leesti, Directive 47 reached for system lore — the wrong tool, which
knows nothing about engineers — and reported its empty result as "no record of an engineer based
in Leesti." Didi Vatermann works there; the engineer table always had them. `find_engineer` now
takes a `system`, named or left out for the Commander's own, and answers who is based there or
says plainly that nobody is. "Engineer in this system," "who's the engineer here" and "which
engineer is here" reach it without a model in the loop.

## 0.110.10 — An experimental effect confirms against its symbol, not its spoken name

"Super Capacitors" never confirmed "Super Capacitor" on a shield booster, because Elite localises
the display name and the checklist was comparing that name rather than the unlocalised symbol
underneath it. Every experimental effect whose localised spelling differs from the recipe table's
was unconfirmable the same way, and the checklist line read the module as carrying the wrong
effect rather than as one it could not check. The comparison now joins on the symbol, and a symbol
the table does not recognise is reported as unconfirmed rather than as a conflict.

## 0.110.9 — Routing and Fleet reach the headset, and a drawn keyboard takes a spelled value

The keyboard a controller ray opens on a text box in the headset heard nothing: every character of
a system name or a carrier figure had to be pointed at. Both drawn keyboards — that one and the
panel's own — now take a spoken value. Say each letter as its word, NATO style, and the keys are
pressed in order: "alpha bravo seven done" types `ab7` and commits, in one breath, because Done is
a key. So are delete, clear and cancel, and digits are said either way.

It is not a mode. Every utterance is tried as spelling first; if any word is not a key, nothing is
pressed at all, the board names the word it could not take, and the whole utterance lands in the
field as a value — so "Shinrarta Dezhra" is said rather than spelled, and "Alpha Centauri" arrives
whole. That gives the headset's board dictation as well, which it also did not have. Spelling is
live only while a keyboard is drawn, so "bravo" in conversation still means what it says.

Ask "how do I spell something", "the phonetic alphabet" or "what is the word for K" and Directive
47 answers from the same table the keyboard parses, without asking a model.

Spelling was the last thing Routing was waiting for. Where you are going was the window's alone:
the plan forms wanted a keyboard, and the headset had none. It has had a drawn one since 0.23.1 and
spoken values into it since 0.25.0, so the reason had been false for a while, and now it is gone
outright. Plan, Progress, Course, Market and Community Goal are all on the headset panel, in the
same place in the tab strip the window puts them, each remembering the reading it was left on the
way every other tab does. A ray press on a form's box opens the drawn keyboard, so a destination is
said whole and a jump range is spelled key by key. Copy and Copy and plot in the galaxy map work
from the headset for the reason they always did: it is the same PC's clipboard and the same key
sequence, and Elite is what is in front of you.

Your ships, their builds and your carrier only drew in the window too. Fleet was withdrawn from the
headset panel during the redesign in 0.37.0 and left out again when Checklist came back, on the
reading that a three-level drill ending in a search field was a bigger surface than one list of
short rows. That no longer holds: every row it drills to is a button or a switch a controller ray
already presses, and every value typed into it goes through the same voice-first prompt the rest of
the panel uses.

Fleet now sits after Transcript on the headset panel, in the place in the tab strip the window puts
it. Ships, Suits, the gap between the two and the carrier all draw there. A ray press opens a ship
and then a slot, the mode switch takes a press, and three grip-backs from a slot row return you to
the root. The hull picture's step buttons do what the mouse wheel and drag do in the window. The
tab redraws from the headset's own tick, for the same reason Engineers does: the ship underneath
you can change without anything else happening first.

Ctrl-dragging one slot onto another stays a desktop convenience. The headset surface has no
pointer-moved path for that gesture to ride on, and no new gesture is being added before 1.0.0, so
a module is copied there by the row's own action.

## 0.110.8 — The empty Checklist names what it is filtered by, and the tab strip decides its own width

"Nothing on your list matches that." never said what "that" was. On the full panel the query box
and the scope button sit beside the message, so the gap was survivable; in mini neither is on
screen, and the query still narrows the list from wherever it was typed. The message now names the
query, the scope, or both, from the same two values the filter itself reads — "Nothing on your list
matches 'limpets'.", "Nothing on your list is in A ship's build.", "Nothing in A ship's build
matches 'limpets'." — and the empty state grows a Clear filter button, since mini has no search box
to clear the query from.

A window that opened narrower than the tab strip's words drew the words anyway, and only
collapsed to marks once the Commander touched the edge — the collapse decision ran only from a
resize, so a window that opened at a narrow size and sat still never made it. The decision is now
made once against a settled measurement before the window is first shown, so it opens in the state
it would otherwise only reach after a drag.

## 0.110.7 — The checklist follows the ship, and Sourcing opens on the headset

Filtering the checklist to the engineer in this system now answers from the system the ship is in.
The page listened to the list, the proposals, the filter and the goals, and to nothing that says
where the Commander is — so jumping into an engineer's system left the empty message on screen with
that engineer's work behind it, and left the partial-grades checkbox and the rank line describing
the system just left. The page redraws when the system changes, and on nothing else: docking,
dropping out of supercruise and every other event inside one system redraw nothing.

Sourcing — where to buy what a build still needs — is now the Checklist's second root on the
headset. It was withheld there because the carrier figure is typed and the headset had no
keyboard of its own; a press on the carrier box opens the drawn keyboard the panel already has,
and the figure is written once when the board closes. The desktop window is unchanged.

## 0.110.6 — Settings rows that open a window

Every Settings row that opens a second window — memories, the debrief, notes, the logbook, the
audio recorder, coverage, ship cores, macros, switches, and the arrow that clears a stored key —
now refuses a headset press and says “Not currently supported in VR” on the panel.
A ray press used to reach the handler, which would have tried to open a dialog over a window that
is never shown. The desktop window is unchanged.

## 0.110.1 — What can I do?

The ask box's empty text is now a question you can actually type. It read "Ask D47 something";
it reads "What can I do?", and on a Commander's first run still offers "where am I" and
"what's your status" beside it.

## 0.75 — placeholder

**The release history for this version was not recovered.** This heading exists because the
checkout is missing its changelog and the build embeds one. See the comment above.

## 0.1.0 — placeholder

**The first release's entry was not recovered either.** Present so the file carries the
newest-first ordering the real one has.
