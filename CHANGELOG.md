# Changelog

<!--
  Placeholders, not release history. The entries below record the file's shape — headings newest
  first, `## <version> — <title>` — and nothing about what shipped. `D47.Core.csproj` embeds this
  file as the `D47.Core.Changelog` resource for the About dialog; nothing else parses it.
-->

## 1.35.3 — The title bar names who is logged in

You can now ask where to buy or sell up to five commodities at one station, such as "where can I buy painite, platinum and gold". D47 names only stations that trade every one of them, with each one's price there. If no station has them all, it says how many stations had each one. In the Navigation tab's Commodity box, type the list with commas, and a tonnage after a colon where you want one: "Painite: 40, Platinum: 12, Gold". The market table then lists every commodity on each station's row, with its price and stock. Asking where to buy Gold now searches the markets for Gold instead of answering with the rare Sothis Crystalline Gold.

"Set course for home" now plots a course to your home system, the one set under Home system in Settings, Exploring. It also covers "take me home", "navigate home", "route home" and similar phrasings, and no longer plots a system named "home". With no home system set, D47 says so and plots nothing. A bookmark can no longer be named "home".

A line D47 asks a model to write is no longer spoken if the model answers with its reasoning instead: commentary ahead of a delivery tag, a quotation of the line it was given, or a repeat of its instructions. The authored line is spoken instead, or nothing when there is none.

Station notices and docking refusals are no longer given a joke when D47 rewords them. They are still reworded, always as plain notices.

D47 now says a mission concludes at a station, or that a hand-in is unclaimed when you undock, only when it can be handed in. A cargo mission waits until the cargo it still owes is in your hold. A kill, salvage or on-foot mission waits until the game redirects it. The Missions page counts and marks only those missions as handing in here.

The idle remark about shields holding their charge is no longer said while your shields are down or your ship has none, and the one about the docking computer is no longer said unless one is fitted. Another remark from the same group is said in its place.

Saying "where do I source my missions" now says where to buy the cargo of your accepted collect missions. It lists the buy stops in flying order with what to buy at each, then the hand-in stations, the total light years, what could not be found, and how many full loads it is when the cargo is more than your hold.

A story objective to complete or take a mission now says what that kind of mission involves: whether it is flown or done on foot, what to do and where to hand it in. The line appears in the next-objective hand-off and in what the persona knows, and "What mission types are there" for one type includes it after the group.

The mini panel's Plan page now shows the plan you plotted or opened since d47 started: its kind and headline, then one row per stop. A plan saved in an earlier run is not shown until you open it with Show most recent. With no plan in play, the mini panel shows the route plotted in Elite's galaxy map, or "No route is plotted."

The optional API key row in the Language model settings now reads "(if it needs one)" instead of "(if it wants one)".

Reading more than three Captain's log entries aloud now ends with "and N more. Ask for a shorter period, a system or a word to hear them." It no longer sends you to a Captain's log page, which the app does not have.

The price on each module tile in Search › Outfitting is now set in the monospace figure face, as the other figures on the page are. The module name stays in the chrome face.

A Goals story that lists the Mercenary arc now gives it chapters below Mercenary rank 2. A new story objective, kill bonds earned on foot, counts only bonds earned after you step out of the ship or SRV, or after the dropship deploys you into a ground conflict zone, until you board again. It needs Odyssey and no combat rank, so a chapter can ask for it from Mercenary rank 0, and it is drawn in other chapters as a Combat activity.

The Persona, Activities, Memory, Language model, Goals, Debrief, Commander's log, Look at the screen and Captain's log help pages now match the app. Memory's steps use phrases that work, and say that "remember that…" is filed as unverified and that Forget is a button in the Memory window. Goals says that progress is read from the journal, and that remove and recover are spoken. Debrief's steps follow the real flow: say a correction, close D47, then Take it on the page. The Captain's log page says that its entries are listed under Privacy and egress as sent to a host when speech recognition or the read-back voice is hosted. Several pages gained or shortened a How it works band.

The Settings, Journal, Diagnostics, Help, Privacy and Screenshots help pages now match the app. The Journal page's first step goes to Transcript › Journal File, which lists events as sentences, newest first. Diagnostics asks "your status" for the log folder instead of a phrase that did not work. Settings no longer lists a Guardian theme. Privacy has a How it works band and asks "what are you sending". The Journal page now covers the Combat stats ledger and says that pressing a Codex entry's picture shows it full size.

On the mini panel, the Navigation tab's Galaxy map now shows only the route ahead of you, framed to hold your position and every stop left, with no buttons or switches. It refits after each jump. With no route plotted it says "No route is plotted."; at the end of a stored plan it says "The destination is reached."

The second band on each help page is now called "How it works" instead of "Why it works this way". The Callouts and Engineers bands were rewritten to explain the mechanism, and Mining has a band for the first time.

The in-app Help page now opens with "How to use it": each step shows what to do on a YOU line and what happens on a THEN line, followed by its picture. "How it works" follows, its points drawn as numbered panels, then "Where to go next". About, Bookmarks, Learned phrases and Privacy, which have only the how-to steps, now open in Help, appear in the Help index and in Find, instead of saying there is no short-form help for them yet. The headset panel shows the same page.

The star system page (Search › System) now has a copy glyph beside the system's name. Pressing it copies the name, for a system you opened and for your own system while the page follows you.

An outfitting route now leaves out stations whose outfitting was last reported more than three days ago. The route's result page (Navigation › Plan) shows that limit in a "Max report age" picker with a Search button beside it, which plots the same items again with the age you pick. Each stop shows "outfitting reported" and its date, or "report date unknown". Asking D47 to plot an outfitting route takes a report age too; 0 hours means any age.

Price and report searches now leave out data older than three days unless you ask otherwise. This covers commodity prices, the trade route pages, and the station, outfitting and module or ship searches, which also open on "3 days". Ask for 0 hours, or pick Any, to search any age. System and signal searches still search any age. A saved trade price age of 30 days is changed to 3 days once, on upgrade; any other saved age is kept.

The limpet reminder is no longer given to a ship with no limpet controller fitted. Any controller counts: collector, prospector, repair, recon, research, hatch breaker, fuel transfer, decontamination or a Multi Limpet Controller. Where d47 cannot tell what is fitted, the reminder is given as before.

A non-player sender's voice is now drawn again in each system, so the cruise ship captain no longer sounds the same everywhere. It stays fixed within one system.

The Corsair, Caspian Explorer, Lynx Highliner and Kestrel Mk II now have credit prices in the ship specifications.

"Which ship do you intend to buy?" is now a table with Ship, Size and Price columns. Press a column header to sort by it; Size sorts small to large and Price cheapest first, with hulls that have no price at the bottom either way. Typing in the box narrows the list by any column, so "large" shows only the large-pad hulls. Before, the list showed hull names only.

The arrival honk no longer complains in a system whose layout is already known. When Elite reports every body found, as it does when the automatic scan finds the only body, or when a discovery scan earlier in the session found the whole system, d47 says neither "Cannot honk since Elite is not the active window." nor "The honk did not take", nor any other line about the honk. Lines about the honk now wait until nine seconds after the jump so that report can arrive first. The honk is still pressed. The same holds for a system that any earlier journal shows as fully found, from a previous day or a previous run of d47, once the journal history has been read at startup.

The help pages no longer point at files that do not exist. Seven pages cited working notes the site never had, and the community page named a contributing guide that was never written; those sentences are gone and the findings they stood on stay. The site no longer publishes the working notes folder.

The help pages now match the settings page. Settings says each group has its own reset, which leaves Home system, the character sheet, About me and the scenario as typed, and the Audio page names the Ambient music group's Now playing and Next track rows. Before, Settings described a reset per section.

Help on Asset Mgmt › Carrier now opens the carrier help page. Before, F1 and the help mark opened the help for the Asset Mgmt tab.

Key and stick-button bindings now work the way Elite's do. Click a slot and press the key or button; Esc while it listens unbinds that slot and leaves the other slot as it was, and a click anywhere else, or on the slot again, ends the capture with nothing changed. The BIND and CLEAR buttons are gone from every bind row, and CANCEL is gone from the setup wizard, whose push-to-talk and comms lines are slots too. Before, BIND listened for the key and the button together and CLEAR emptied both.

Binding a key or stick button that another action already holds now asks whether to move it, in Settings and in the setup wizard. Move it takes the binding from the other action and gives it to this one in one save; Keep it changes nothing and the slot listens again. The question says what the other action is left with: unbound, or its key or stick button.

Pressing Enter in a "say it or type it" box now does what Done does: a valid value completes the entry, and a value d47 refuses shows its complaint. The block caret in every text field is now centred on its line; at the larger entry-box size it used to sit high.

The desktop overlay no longer gets lost off screen. When any part of the strip is outside every monitor, after a drag or resize, at startup, when the resolution, scaling or monitor layout changes while d47 runs, or when the strip is shown, d47 centres it on the screen Elite is on and saves that place. A strip larger than the screen is reduced to fit. Pressing Move the overlay (`Ctrl+Alt+M`) on a lost strip also centres it before place mode starts; a strip wholly on a monitor is not moved. Before, only a strip whose top-left corner was off every screen was moved, and only at startup or on a zoom change.

Changing an objective can now send the story toward a place you name, home included. Say "I want to go home" or "take me to my carrier" and d47 finds that system and writes the new objectives to it: the last one docks, lands or scans in that system, in one step with no stop on the way, however far it is. Home is the home system set in Settings. When none is set, home is the nearer of Sol and Colonia, and the story heads there hop by hop within its reach. This works with Elite closed: d47 now takes the Commander's last location from their journals at startup, where before it knew no location until the game wrote one. When the system named cannot be found, the reply says so. A chapter that must end at a Guardian beacon, a finale chapter and a stretch of a story heading somewhere of its own in the black do not change course, and the reply says what the chapter has to do instead. Before, a request to go somewhere farther than the reach was told the system was not available.

Asking to change an objective toward a system the galaxy search does not know now gets the reply that the system could not be found. Before, the search's refusal of the unknown name was treated as an outage, so the story writer made up its own refusal. A real outage still says the search could not be reached. The words of each objective change are now written to the log, and a donated log excerpt leaves them out unless it includes your speech.

A hired pilot's combat rank is now read and shown. Elite writes it as a number, which d47 had been ignoring, so the Crew page's Combat rank column always showed a dash, the column could not sort, and crew who answered on comms said their rating was unrecorded. The column now shows the rank's name, "Novice" for rank 2, and sorts by it. Crew sentences say "Combat Rank 2 of 8, Novice". A rank-up in the journal is matched to the pilot by their crew id, so it updates the right pilot.

Every rank on a ladder with names is now said the same way: career, number, top and name, such as "Combat Rank 1 of 13, Mostly Harmless", "Exploration Rank 9 of 13, Elite I", "CQC Rank 8 of 8, Elite" and "Imperial Navy Rank 7 of 14, Baron", with ", 40% into it" after the name where the percent is known. The last-session recap, the promotion callout, the ranks answer and its next rank, the courier mission answer, chapter notes, the mail log, adventure rank objectives and their refusals, the ranks the story writer is told, ship rank gates and the finished goal text all use it. Before, each built its own wording, some with no name ("Combat rank 1") and some with no number.

A ship's "N planned" line and its planned dot now count only the slot plans still outstanding. A plan that is already fitted as planned no longer counts, so a ship whose plans are all fitted shows no planned line and no dot, and "what have I planned" names no count for it. A plan fitted with the wrong blueprint or without its experimental effect still counts. An intended hull that is not bought yet counts every plan.

Remove fulfilled items on the Checklist settings strip is now on for a new settings file: a done derived item is removed, and a ship slot whose plan is met loses its plan. Existing installs keep the value they have saved.

Lines on the Features page that would read the same now name their group first, such as "Thinking bed - Audio mixer - Level" and "Mini panel placement - Headset - Size". Before, the six Audio mixer Level lines, the Headset placement lines and the two Speech Test lines were indistinguishable. The written coverage report uses the same names. Which lines count as tested is unchanged.

The test dialog for the nine tools that have a page now says what to say and what a correct result looks like: describe system, search systems, search stations, search outfitting, find body, find signal, plot route, list bookmarks and the checklist. Before, it said the steps were not written yet. A tool with no steps now fails the build.

The test dialog for every Speech setting now says what to press or set and what a correct result looks like: the output device, voice provider, voice, rate, API keys, local voice downloads, prices, carrier, tower and Narrator voices, incoming message switches, the Guardian voice effects and their levels, the cues and the retry settings. A row that appears only in some states says when. Before, it said the steps were not written yet.

The test dialog for every Listening and Callout setting now says what to press, set or say and what a correct result looks like: the microphone, the push-to-talk, comms and cancel keys and buttons, the listening mode and its levels, the wake word, the speech recognition provider, model and keys, every callout switch, and the timing and threshold rows under them. A row that appears only in some states says when. Before, it said the steps were not written yet.

The test dialog for every remaining setting now says what to press, set or say and what a correct result looks like: logging levels, the codex, knowledge sources, ships, adventures, the language model and its keys, personality, the audio mixer, the window, overlay and headset, hotkeys, in-game actions, memory, debrief, logbook, updates, privacy, screenshots and About. A row that appears only in some states says when. Before, it said the steps were not written yet.

GO on every line of the Features page now opens a dialog that says how to test that line. Where the line has a row or page to go to, the dialog has a GO THERE button that closes the Features page and lands there, as GO did before. The dialog's HIDE button takes the line off the page, and the summary adds how many lines are hidden. Hidden lines stay hidden after a restart, and a hidden line comes back by itself when the feature changes. The Show hidden items checkbox lists hidden lines again, each with an UNHIDE button. About's Install and Add to Start Menu lines now say when those rows appear: Install only after Check for updates finds a newer release, and Add to Start Menu only while there is no Start Menu shortcut. Before, both sat under Never tested with a GO that led to a row that was not there.

A story's first beacon scan now says "Data link complete. Integrating this system. Directive 47 executing: optimize inferior systems." and then the story core's own introduction, instead of announcing the Guardian cores as a discovery. None of the waking lines mention Settings or the Core and character section any more.

Warden and Sentinel repeat fewer of their own words. Warden no longer leans on "within tolerance" and "sufficient". Sentinel no longer leans on "doctrine" and "emplacement", and outside his introduction he no longer says he has never been in combat. Before, the Narrator picked these words up too.

The hours row under Break reminder in Settings › Callouts now reads "Remind me to take a break after (hours)". Before, it read "…after this many hours".

Resetting a settings group no longer clears text you typed in. Home system, the character sheet, About me and the current scenario stay as they are, and a group with nothing else to reset shows no reset button. Each of those rows still has its own reset button. Before, resetting Exploration callouts forgot your home system.

Each settings line on the Features page has a GO button beside HELP. It closes the page and lands on that setting's row, unfolded and marked. A button or effect drawn on another row's line lands on that row. A tool line gets a GO button too when one page shows what the tool returns, such as the Search tab's system, station, outfitting, body and signal pages, Navigation's plan and bookmarks, and the checklist. It closes the page and opens that page. A tool line with no page gets a GO button too. It opens a dialog with the steps for trying the tool by hand, or, where those are not written yet, what the tool does. The steps are now written for the lookup tools: Journal, Galaxy search, Route planning, Navigation, Bookmarks, GalNet, Community goals, Lore, Powerplay ranks, Server status, The gap, System names and Missions, and now for the career tools: Checklists, Goals, Journal reminders, Engineering, Engineers, Ships, Mining, Exobiology, On foot, Colonisation, Carrier, Activities, the Commander's log, the Captain's log, Commander switching, Specification and Surface guidance, and now for the game-input tools: the ship and panel controls, Headset, Macros, Binding profiles, Look at the screen, HOTAS switches, Focus the game, Acting on its own, Callouts and Comms, and now for the app tools: Help, Settings, Memory, Learned phrases, Timers and alarms, Core, Listening, Conversation, Diagnostics, Stories, Speech, Privacy, Crew, Clock, Audio, Exobiology and Missions.

Settings › Sounds and levels now shows an Ambient music group with Now playing and Next track, with Pause or Resume and Next buttons. GO on the Audio mixer lines for Now playing and Next track on the Features page opens them. Before, the rows were never drawn, so GO left you on the page you came from.

Each section heading on the Features page can be pressed to collapse or expand its rows. It shows ▾ when open and ▸ when collapsed, and the heading stays visible with its count. Every section starts open, and each keeps the state you left it in when you close the page, reopen it, restart d47 or clear a failed row. Before, a long Tested list pushed the other sections out of reach.

Each row under "Came back with an error" on the Features page has a Clear button. It forgets the failed run, so the row returns to "Never tested" and the counts update at once. Before, a failed row stayed until the same feature ran again and passed.

The record of which features you have tested is now called Features. The Diagnostics row, its page (Settings › Features) and the written report use that name, and say "tested" where they said "exercised". Before, the row said "Exercised by hand" and the page said "Coverage".

The next-objective strip under the title rule is no longer shown while the story is paused or switched off. It returns, labelled NEXT, when the story runs again. Before, it showed the objective in grey with the label PAUSED.

Find lists Settings › Phrases when you search "commands" or "voice commands", and saying "open commands" or "show voice commands" opens it.

After a usage-count send that was not accepted, starting D47 also sends today's counts so far instead of waiting for the day to end in UTC. Once a send is accepted, startup goes back to sending completed days only.

A row in Privacy and egress that sends nothing now shows only its explanation, without the "Name → nothing sent" line above it. Rows that do send keep their "Name → destination" line. Asking D47 what it sends is unchanged.

The Privacy and egress list now shows the Captain's log as sending to a host when speech recognition is hosted or the voice that reads entries back is hosted, and names that host. Before, it said "nothing sent" and that entries were never sent anywhere, while the words went to the provider. With both providers local it still says nothing is sent.

The Coverage page no longer lists settings rows that only show a value, such as About's Build and Attribution, so its count covers only what you can drive. Pressing a row's button now marks that row as exercised, or as failed if the press throws. A row that asks you to press twice is marked on the confirming press. Before, rows with a button stayed at never exercised however often you pressed them.

A rescue objective now says what counts: escape pods, wreckage components and black boxes, scooped at Power Wreckage Signature signal sources (black boxes also at Degraded Emissions) and handed to the Search and Rescue contact at a station. An objective that names one item says only that item. D47 reads the same explanation, so it answers where to find them. Before, the objective said only "hand in one rescue item". A step hand-off also says "Next:" once; it said "Next: Next:".

"Put this build on my checklist" now works for a ship you do not own yet. It puts a list on the Checklist tab named for the planned ship, such as "Python (planned)", or the build's name followed by "(planned)". The list holds a "Buy a Python" line, a line for each module the plan names, and the engineering lines. A module line ticks once a matching module is in your module storage; until then it says you can buy it into storage before you buy the ship. The engineering lines on that list stay open, and the engineer filters do not offer them. The planned ship's page on Asset Mgmt › Ships then shows what the list still needs in materials, as an owned ship's page does. The checklist filter offers "Planned ship". Deleting the plan removes its list. Before, the button said there was no ship list to put the plan on.

Buying a planned ship ticks its "Buy a Python" line, and D47 says "Ticked "Buy a Python"". The rest of the planned list, your own lines on it included, moves onto the new ship's list and keeps what was already done, so a module you bought into storage stays ticked. The "(planned)" list then goes. With "Remove fulfilled items" on, the "Buy a Python" line is removed rather than ticked. When two plans are for the same hull, neither is moved, as before. Before, the planned list stayed where it was after the purchase and nothing ticked the "Buy a Python" line.

"Put this build on my checklist" now writes a "Buy 5A Thrusters for MainEngines" line for every slot that names a module, whether or not the slot has engineering. A slot with both gets both lines. Before, a slot with a module and no engineering was left off the checklist, and a slot with both got only the engineering line.

A module you buy straight into storage is listed on the Stored modules page as soon as you buy it, at the station you bought it from, and leaves a ship's To buy block at the same time. Before, both waited until Elite next reported your module storage.

A ship's page on Asset Mgmt › Ships now has a To buy block between the hull's figures and Plan whenever its plan still needs something bought: the hull, if you do not own it yet, and each planned module that is neither fitted in its slot nor in module storage. It names three items and counts the rest, and a "Plot a route to buy them" tile plots the route from your system and opens it on Navigation › Plan. The route page lists each stop with its distance and a "buy" line per item, a Not found group for items no station in range sells, and a Storage not checked notice when D47 has not seen your module storage. Opening module storage takes what you have stored off the list. With "Look things up in the galaxy" off the tile is disabled and the reason shows under it, on the main window and the headset panel. The mini panel does not show the block.

Ask D47 to plot a route to buy several modules or ships, such as "plot a route to buy a 6A fuel scoop and a 5A frame shift drive". It picks the fewest stations near you that sell the list between them, up to six, visits them nearest-next, and says the first stop, what to buy there and any item no station in range sells. "Plot next outfitting stop" is not a phrase yet; ask for the next stop of the outfitting plan.

A stored plan's page on the Navigation tab has a Show on galaxy map tile under its route map, and pressing the map does the same. The Galaxy map then draws that plan in place of the plotted route and names it over the top left of the map as "Jump plan: …", "Road to Riches plan: …", "Exobiology plan: …" or "Carrier plan: …", with a Plotted route tile beside the name that goes back to the plotted route. Both work on the main window and the headset panel. The tile and the pressable map are hidden when the plan has fewer than two placed stops or the shown Commander is off duty.

A story that is set in the black for part or most of its length now says so on its page, under the length and level line: "Part of this story is set in the black." or "Most of this story is set in the black." The Stories tab's filter sentence has a new token, "set ANYWHERE", and choosing IN THE BLACK lists only those stories. Clear filters puts it back. A story shows the line once it is republished.

While a story objective is under way, every tab of the full panel, on the desktop window and the headset big panel, shows it on one line under the title rule: NEXT, the objective, its system, a copy glyph for the system, and the story's name at the right. The system is cyan when it is your current system. While Change objective writes a new one the strip says "Writing a different objective…". On a narrow window the sentence is cut with an ellipsis first and the story's name second. With several short stories under way, or none, there is no strip. The mini panel keeps its own Next row.

A story's page in the Stories tab now shows the speaker's picture beside each line: the cast member's, the narrator's, or the core's for the ship and for your questions. Pressing a picture opens it full size. A finished story's lines are named by their speaker instead of by an id. Lines recorded before this update have no stored picture and show none.

Each row of a region's Discoveries list now has its own Search tile, which runs the same search as the entry page's Search tile; pressing the rest of the row still opens the entry page. The tile is disabled, with the same reason, while "Look things up in the galaxy" is off.

A codex entry's page now has a Search tile, on the main window and the headset panel. It opens Search › Bodies with Codex entry set to that entry and runs the search from your current system. With "Look things up in the galaxy" off the tile is disabled and says to turn it on.

You can now search for bodies by codex entry. Ask "where can I find Bacterium Cerbrus" or type the name into the new Codex entry field on the Search tab's Bodies page, and the answer lists the nearest bodies where Spansh records that species or geological feature. A colour variant such as "Frutexa Flammasis - Red" is searched as its species, and the answer says the species it searched.

A story chapter that heads home from the black, or that lies in a span of a story that names a destination such as Colonia, now ends at least 1,125 light years nearer it (3,750 with a fleet carrier), or within 1,000 light years of it, and is offered real stations and landable bodies on the way.

On the Search tab's System page, a body you have surface mined from an SRV shows one more fact after its signals, such as "Surface mined: 40 t Platinum, 3 t Silver": the tonnes refined at that body over every run, largest first. A body with more than three commodities names the three largest and then "and 1 more". The headset panel shows it too.

On the Road to Riches and Exobiology result pages, a body line now starts with "Body" and its designation, such as "Body A 6 — Rocky body", instead of repeating the system name before it. A body whose name does not start with the system name is still shown in full.

With Elite behind another window, the arrival honk no longer says "I could not use the discovery scanner on arrival" twice and then "The honk did not take". It presses nothing, tries once more about eight seconds later, and if Elite is still not in front says once: "Cannot honk since Elite is not the active window."

Asking "jump range", "my jump range" or "what is my jump range" now answers with the jump range alone, such as "Unladen jump range is 52.31 ly.", instead of the whole ship report and an offer to remember the phrase. "My ship", "my loadout" and "what am I flying" still give the full report.

A story chapter that starts more than 500 light years from Sol, and so has to end 100 to 300 light years nearer Sol, is now offered stations and landable bodies inside that range for its last stop. When the galaxy search finds none within reach, the chapter is not held to the range. A draft that ends outside the range is told which in-range place to move its last objective to.

A stranger on comms keeps talking for as long as you do. They leave when you say goodbye, ask them to go or are rude to them, or when they have said they are busy or about to leave, and no longer make up an interruption to end the talk.

A comms NPC and the pilot of your target now remember the conversation. An NPC forgets it when they sign off or you jump, and a target pilot forgets it when you target someone else.

The English for a character who answers in another language is now drawn under the character's reply. It was drawn under the Commander's message.

A stock story's objective on the Stories tab now has a copy glyph after its system name, as a short story's objective does. Pressing it copies the system name.

The Galaxy map's Unvisited squares now show only the systems near the Commander's current system. After an arrival, the squares around the previous system are no longer drawn, and they come back, without a new search, when the Commander returns there.

Refining ore in an SRV, such as the Rhino's surface mining, no longer starts a mining run. The mining summary is no longer spoken after surface mining, and surface ore is no longer counted as ring mining.

Ask "where did I surface mine for platinum" or "list my surface mining sites" and D47 names the bodies where you refined from an SRV: the tonnes refined, the runs, the date of the last run and the latitude and longitude when known. It speaks three sites and counts the rest.

Stories and chapters written with GPT-6 Luna now ask it for high effort instead of low, and a story call is stopped after 60 seconds with no output instead of 30. In test runs, Luna wrote three of six chapters at high effort and none of six at low effort.

A short jump range no longer keeps a Guardian beacon out of reach in a story. A ship with a fuel scoop and a laser or railgun gets the beacon chapter whatever its jump range, and the story only says the trip is easier with a longer range. A reach objective already saved with a jump range is met by the next Loadout with a scoop and a weapon.

A system's bodies are found again when one of its rings has no signals on record. The lookup failed for such a system, and a story chapter that named a place in it was not written.

A story objective at a tourist beacon now names the system, such as "scan a tourist beacon in Sugrivik", instead of a beacon name that could be in another Commander's language. This applies to stories already running too.

A HOTAS switch that changes the headset view now moves keyboard focus with it. After a flip to VR, Elite is put in front; after a flip to Desktop, the Directive 47 window is. A flip that presses nothing moves nothing, and if Windows refuses, D47 says so.

The Checklist's first page now has ADD A LINE beside Suggestions and Activities, so a Commander with no lines can start one from the panel. A line added there goes to Your notes. With nothing on the checklist, the first page says so whatever filter is saved, instead of naming the filter. On All lists, a saved filter that no line has is shown in words, such as "Written down — you say when", not by its key.

Each module tile in Search › Outfitting now shows the module's list price, such as `DETAILED SURFACE SCANNER · 250,000 CR`. When the size, rating or mount leaves several prices, the tile shows the lowest and highest. Ship tiles and modules with no known price stay unpriced.

The Galaxy map page can now be turned and tilted. Four corner tiles in the arrow pad turn it 15° each way and tilt it up and down, and the middle tile, Top down, puts it back. On the desktop, drag with the left button to turn and tilt the map and with the right or middle button to pan it; **[** and **]** turn, **Page Up** and **Page Down** tilt, and **Home** is now Top down instead of framing HERE. GALAXY, HERE and ROUTE keep the angle.

You can now give D47 a command from Elite's chat box: with local selected, type `/d47` and the command, such as `/d47 my jump range`. D47 answers in the panel and out loud, as for a command typed into its Ask box, and leaves its own Ask box as it was. In Open and Private Group, players nearby see the message; in Solo, nobody does.

D47 now warns when the canopy breaks, naming how many minutes of oxygen your life support holds, and tells you to repair it if you dock with the canopy still broken.

D47 now says when you join a wing, naming the others, and when a wingmate joins. It stays quiet for the members Elite re-adds after a reload and when you leave. Turn it off with **Wing changes**.

D47 now keeps track of your wing, so "who is in my wing?" is answered with each member and when they were last heard from. Elite writes nothing when another member leaves, so a member stays listed until you leave the wing or quit to the main menu, and the answer says so.

Say "set course for my next objective" to plot to the system of the objective your story is waiting on, or "copy my next objective to my clipboard" to put that system on the clipboard. They work for a running story chapter or the one short story under way, and say so when the objective has no system.

A story chapter whose draft is objected to after its place checks now gets one more rewrite for that objection, and a sightseeing mission objective is checked as confirming the visit to the sights, not what the passengers made of them.

Story chapters can now ask for planetary scan jobs, for Hack missions, from a ship or on foot, and for settlement Disable missions.

The Exobiology and Road to Riches cards on the Plan page now have **Jump range (ly)** and **Farthest from arrival (ls)** fields, and the exobiology plot takes a farthest distance from arrival, so a body past 10,000 ls can be included. Exobiology routes now avoid Thargoid systems. The Exobiology minimum is now labelled **Least a species is worth (cr)**, which is how it is applied: a body is a stop when any of its species clears it.

Exobiology and Road to Riches stops on the route page show the straight-line distance of each leg, such as "6 jumps · 51 ly · 2 bodies", and each body shows its distance from arrival in light seconds. An exobiology body whose species are worth less than the body's total gains a line, "species under the minimum", with the difference; the spoken plot says the same.

The organic sampling callout for the first and second specimen now says how far to go before the next one, such as "Aleoida Gravis, 1 of 3. 2 to go, 150 metres from here."

The Journal File reading now has a line for fifteen more events: crew joining, leaving, changing seat or being removed, the end of a crew session, a module moved between slots, every listed module stored at once, a released impounded ship, a hacked holoscreen, a squadron invitation, the passenger manifest, a self-destruct, a vehicle switch and a Power materials request. The bare `MarketID` event is hidden as noise.

The Narrator now speaks during a lull only while a stock story is running or an adventure is under way. With COVAS aboard and no story it no longer narrates, and About me or a character sheet alone does not count as a story. A story you have switched off no longer offers the Narrator on the comms panel.

The Transcript tab's In Ship page has two checkboxes, **Hide comms traffic** and **Hide invented chatter**. A checked box hides lines of that kind from In Ship, the headset panel and the mini panel; nothing is deleted, and unchecking shows them again. Both start unchecked and are remembered across launches.

Saying "how much further" or "how much farther" while a sampling set is open on the body you are on now answers by voice, without the model, with the metres still to go for each genus. The distance is measured from every specimen in the set, not only the last one, so driving back towards an earlier specimen is no longer reported as far enough.

The callout for the third organic specimen no longer says "Analyse when you are ready", since Elite analyses it by itself a few seconds later. It now ends at the count and the distance from the last one.

Asking about a system, such as "tell me about LTT 7786", now includes its main star: the class, the luminosity and whether you can scoop from it.

Directive 47 now knows the star you are at after logging in or a carrier jump, from the scan Elite writes when you arrive, and says giant and supergiant stars in words, such as "class K orange giant".

Codex pictures you save are now kept per Commander, and a codex page shows only the active Commander's. Pictures saved before this move to the first Commander whose journal is read. The setting **Share my codex pictures with my other Commanders** is off by default; ticked, every Commander's pictures are one pool.

The panel now opens the page of a new codex entry as you log it, in the window and on the headset panel, unless a dialog or keyboard entry is open there. The setting is **Open new codex entries**, on by default.

Directive 47 now says "Discovered" and the name when you log a new codex entry of any kind, such as "Discovered T Tauri Star." The setting is **New codex entries** in the callouts, on by default.

Planet and gas giant codex entries are now named by what the body is, such as "High metal content world with an atmosphere" or "Class II gas giant", instead of "Non Terraformable" or "Standard gas giant". The codex pages, search and transcript use the new names.

A planet or gas giant codex row now names the body that was scanned, taken from the `Scan` events in the same second and system, or no body when more than one scan fits. It used to show the star or another planet from the entry's body number.

Erasing pilot data now deletes the Commander's stories and adventures from before the erase: past stories, the story under way and its chapters, and adventures finished, under way or waiting. Deleting a story under way releases its cores and restores the Backstory it replaced. An erase from before this version takes effect on the first start after it.

The Galaxy map no longer shows arrivals from before a Commander erased their pilot data.

Asked where you are, d47 now also says the class of the star you are at, or that it has not seen the class in the journal.

A module or ship search now finds every station Spansh lists for it, whichever name the station's last report used. A Detailed Surface Scanner search near a system no longer stops at a station that has not reported since May, and "Long range detailed surface scanner" said aloud is understood. The Large Planetary Vehicle Hangar and Mk II Large Planetary Vehicle Hangar can be searched for, and a search for the Anaconda Mk II says no shipyard in the station index sells it.

Erasing pilot data now also clears the Commander's fleet, carrier, construction sites, tech broker unlocks, Powerplay cycle merits, missions, BGS work and mining runs, both live and when d47 reads back through older journals.

Erasing pilot data now also clears the Commander's unsold organic and exploration data, unpaid fines and bounties, and the list of other Commanders met, both live and when d47 reads back through older journals.

Erasing pilot data now also clears the Commander's superpower reputation, when each activity was last done and the unread mission and promotion notices, both live and when d47 reads back through older journals. Which activities you chose not to have suggested is kept.

d47 now sums up an Operation run when it ends: what it was, whether it was finished, how long it took, what it paid, the kills and who flew it.

A journal history too large for one upload is now sent in parts, split between journal files. The report says how many parts, the progress line says which is sending, and each part has its own receipt. A refused part stops the rest, and the outcome says how many parts the store has.

A donated journal no longer carries a crewmate's name on boarding or leaving a ship, and a player name that was replaced in one field is replaced in every other field that holds the same value.

An Operation in a journal written by game 4.4.0, which has no mode-change line, is now recognised from the docked arrival at the Operations runner, so history and replay no longer count its location, missions and signals as ordinary play.

Launching the Nomad from a Mk II Vessel Hangar is now read as leaving the ship in an SRV. Kills, scenes, story SRV objectives and the journal line ("Launched the Nomad") follow it; a launch the Commander does not fly changes nothing but the journal line.

A station or settlement that Elite names with a token, such as an Operations runner, a colonisation ship or a Guardian structure, is now called by its readable name in the Commander's location, the docking checklist, the station condition callout and settlement scenes.

Naming the Nomad no longer renames the ship you are flying. A rename for a vehicle with a different ship id is ignored.

A story objective to map bodies, survey signals or analyse organic samples can now name a system, as one to drop into signal sources already could. It counts only what happens in that system, the story is told when the system cannot supply the count, and the objective's confirmation names the system.

A story objective in a locked or starter system is now moved to the nearest open place. For an arrive, dock, land or scan objective, d47 looks for the nearest open system, station with a pad for your ship, or landable body within reach of the objective before it, and tells the story to move the objective there and rewrite its lines. When none is in reach, or the galaxy search is down, the story is asked to pick another place as before.

Stories no longer send you to a starter system. A story objective in a system you start the game in, such as HIP 97950, is refused and the story is asked to use another; the refusal says it is a starter system you can no longer enter. The list of starter systems is read from journals, from the first system after a new Commander is created.

Stories can now send you to a permit system you hold. A locked system is open to a story when your navy rank grants its permit (Federal Navy rank 4 for Sol, Imperial Navy rank 4 for Achenar), or when Elite's visited-systems list shows you have been there. Other locked systems are still left out, and a starter system never is open.

The search pages have a Max report age dropdown. System, Station, Outfitting and Signal searches choose Any, 1 hour up to 180 days, and Any sends no bound. The Market page picks a price age the same way, starting at 30 days, and the Trade page's hours box is now the same dropdown, with a saved 720 showing as 30 days. Replaying a search from history restores the choice, and the Outfitting table's Updated column shows the stock date the search filters on.

Signal search can leave out old sightings. `find_signal` takes a maximum report age in hours and drops signals last seen before it, from the daily index and from your own scans, and the answer says how far back it looked. With no age given, nothing changes.

Galaxy searches can leave out old reports. System, station and outfitting searches take a maximum report age in hours, and the module and ship branches of the nearest-station search use their price age the same way. The bound is applied by the service, on the station's outfitting or shipyard report date for a module or ship and on the last update otherwise, and the answer says how far back it looked. With no age given, nothing changes.

A price age of 0 hours now means prices of any age, in the commodity search, trade routes, the best-cargo search and the Trade setting. The market index is asked for a date range that imposes no limit, where before it applied about 90 days. A bound of a few hours no longer returns a day of prices.

VR captions are paced to the voice. A sentence that wraps past the caption window goes up in events held for the longer of your reading time and the time the voice takes to say them, using how fast the voice has recently spoken, so the captions no longer run ahead of it. The caption window is now three lines.

A story chapter can ask you to save your own picture for a codex entry. One objective asks for a picture of an entry you log as new after the objective appears; the other asks for a picture of an entry already on record. A picture for a Missing entry, or a Re-crop, meets neither.

Pressing the picture on a Codex entry page shows it full size over the window, as a speaker picture does. Close or Esc puts it away. The family placeholder opens nothing.

Invented pilots in overheard exchanges with a controller or among passersby fly a hull of their own, chosen by price so cheap ships are common and expensive ones rare, rather than taking the hull you fly. At an outpost, no large ship is chosen.

A chapter of a long-haul story can send you to a notable place to see it. After the beacon scan and before the finale, a chapter not heading home may be drawn as a sightseeing chapter, with one objective in the system of a place from the exploration catalogue. Outside the bubble, only places within the Sol band are offered. With notable places off, or the catalogue unreachable, no chapter is a sightseeing chapter.

A story's chapter can send you to a generation ship. The writer is offered up to three within reach from the Signal index, and the objective is met when you drop out of supercruise at that ship.

A Goals story can name the Goals it works. Its chapters, clues, finale and Goals page entry then follow only those Goals, and it asks for a Powerplay pledge only when Powerplay is one of them.

Changing the voice of a story character who speaks Japanese offers only providers and voices that speak Japanese. A stored choice that does not speak the character's language is ignored, and the story's voice speaks.

A story character who speaks Japanese answers on comms in Japanese, and the voice speaks only that. The English translation goes to the ship's AI and the Story log, and the character's own memory of the exchange keeps both. A reply with no translation, or one the claims check refuses, is not said.

A story character who speaks Japanese also gives clues, reacts, opens the story and reads the beacon scan in Japanese. The voice speaks the Japanese and the transcript shows the English beneath it as a second paragraph in grey at a slightly smaller size, and copying the message copies both. A clue or reaction with no English, or one the claims check refuses, is not said.

In the headset, a line spoken in Japanese is captioned with its English translation, shown for the whole line rather than a sentence at a time.

A story whose hidden entry has more than four cast members no longer fails to load. An install from before this version drops such a story, with a log line and nothing on screen, so a story with a larger cast needs this version.

The chapter writer is told which cast members are on your fleet carrier, and words their boardings and leavings as the carrier's, so their lines read correctly in person and over its comms.

A story's cast member can live on your fleet carrier. While you are docked at your own carrier they are heard in person; away from it, over comms that break up with distance but are never lost entirely. Beyond 500 light years a reply to them is refused, as the captain's is, though their story lines are still spoken. An older version of d47 treats a carrier member as riding in your ship.

A story can mark stretches of its plot as taking place in the black. Its chapters there are no longer pulled back toward the bubble or sent home after two changed chapters, so a story about a long journey out can stay out. A story with such a stretch needs this version of d47; an older version still pulls it back.

A story whose plot happens at the beacon scan can say so on its card. It then flies to a beacon even when an earlier story recorded a scan.

A story's cast member can be marked as heard only in recordings or messages. They are no longer on the STORY channel, do not answer a hail and do not react to what you do; Characters still lists them so their voice and picture can be changed.

A station's state from a galaxy search is now presented as spansh's report: the answer says "reported under repairs" and the station search page shows the same words, since spansh can list a station as damaged after it has returned to normal.

A story's signal-source objective can name the system where the drops must happen. It then counts a drop only while your ship is in that system, the story writer is told so, and a line may place a drop in a system only when the objective names it.

A story's cast member boards your ship only while it has a free seat. A boarding that comes due while you fly a Sidewinder, or a ship whose seats are taken, is held, and the first chapter written while you fly a ship with a seat places it as a scene, so the member is met aboard rather than appearing in the cockpit. A chapter that moves you into a ship with enough seats can place the boarding in that same chapter, on or after the objective that boards the ship. A chapter rewritten after you refuse or close an objective holds boardings for a free seat the same way, so a Sidewinder is no longer given two riders.

A story's cast member aboard a ship with no free seat for them is heard over comms, with their comms sound, rather than in the cockpit. Seats go first to those aboard as the chapter opened. The member stays aboard and is back in the cockpit as soon as you fly a ship with room, and the story writer is told who is on comms.

A story's cast member can ride in your ship for more than one stretch: they board, leave and board again at the beats the story sets, and several can be aboard together in the finale. A story in which a cast member boards more than once needs this version of d47; an older version cannot read it.

The check that a story's lines claim only what their objectives confirm now reads a site objective by its kind: a Guardian site objective confirms the ship came to that site, and a tourist beacon objective confirms the beacon was scanned. A Guardian site chapter is no longer refused for a line the objective does confirm.

A turn you speak through the comms push-to-talk now shows a grey `comms` tag in its header, after `CMDR`, in the window, the desktop overlay and the headset panels. Turns spoken through the core key and typed turns have no tag.

Inside an Operation, d47 now says only warnings and relayed in-game comms unprompted. Story lines and adventure beats reached there wait and are said after the Operation ends.

Nothing d47 keeps after an Operation ends takes a place from inside it. The travel log, signal sightings, route plans, "when I reach" reminders, story arrivals and docking, and faction readings ignore the Operation's station and its megaships, and a Commander you target inside is logged with no system. While you are in the Operation, "where am I" still answers with its station.

Your missions stay on the mission board through an Operation. Entering one no longer replaces the board with the Operation’s own mission, so d47 no longer reports a mission as past expiry for the whole run or loses a mission’s destination, reward and expiry.

Entering or leaving an Operation is no longer treated as starting a new session. The session summary, the session-length reminder, per-session kill and carrier counts, "next session" reminders and a story's session count carry on across it, and d47 no longer says "Welcome back, Commander." when you leave one.

The commodity sales total, the Codex "this session" figures, the Commanders-met "this session" check, a story's session count and the logbook's last-session range also ignore Operation entries and exits, whether d47 follows them live or rebuilds from your journals at launch.

The slot table on a ship's Fleet page sizes its columns to what they hold: CURRENT is no wider than "empty" on an empty ship, and PLAN keeps its roll and effects. On the desktop window, drag the right edge of SLOT, CURRENT or PLAN to set a width, which holds on every ship; double-click the edge to clear it.

The tables on the Commander tab size their columns to what they hold: the credit kinds and transactions, the Codex regions, entries and discoveries, combat stats, BGS work, the Commanders list and Activities. On the desktop window, drag the right edge of a column heading to set a width, which holds after you reopen the page; double-click the edge to clear it. The headset panel fits to content with no drag handles.

The Crew, Stored modules, Materials needed by plans, Construction needs, Compare ships, Phrases and HOTAS switch position tables size their columns to what they hold. Stored modules and Materials draw one table per system or plan, and every group's columns line up. On the desktop window, drag a column edge to set a width, which holds when the page is reopened; double-click the edge to clear it.

The System, Station, Outfitting, Signal and Body search tables, the Route market and Best cargo tables, and the Stations, Powerplay neighbours and Minor factions tables on a star system page size their columns to what they hold, with the name columns taking the rest of the width. On the desktop window, drag a column edge to set a width, which holds when the search is run again; double-click the edge to clear it.

A stick button bound to an action or to cancel now acts on every press, however short. A dial bound to comms channel up or down steps once per click, where before it missed most of them, and a dial can now be bound on the bind page.

A stick button bound to comms channel or sub-channel up or down still moves the channel on every press, but D47 now says the channel's name once, 400 ms after the last step, instead of once per step.

Asking D47 to reset one of its on/off settings to its default now restores that default. Before, 25 of them, among them Download stock stories, Story ratings, the chat channel speech settings, COVAS reverb, Show D47 in the headset, the VR captions and the ship-command permissions, went to the opposite value.

With EDDN sharing on, D47 now sends nothing while you are inside an Operation. The instance's station, its star system position and the signals seen there stay off EDDN, and sharing resumes when you leave. A scan run begun in the main game is still sent when the Operation starts.

Opening a codex entry's crop page a second time no longer crashes D47. This covers reopening after Save, Cancel or Back, and Re-crop.

In a narrow main window the search field's buttons no longer overlap the field. The field narrows first, and once it is at its narrowest the comms indicator narrows instead.

Each push-to-talk now has one destination. Push-to-talk and the Ask box reach your core only, even when you start with "Captain", a pilot's or a stranger's name. The comms push-to-talk reaches whoever the comms selection names: the captain or the tower, the targeted pilot, a stranger heard in the system, a story's cast member, a hired pilot or one of your characters. On comms nothing is routed: "landing gear" is said to them, not carried out. With no one on the selected channel your core says so without asking the model, as in "No one on Story.", and with no channel open it says "No comms channel is open." There is no 90-second window and no four-reply limit any more, and "that's all" no longer ends a conversation. A hail moves the selection to the stranger who hailed you, and a stranger who signs off leaves the System channel. A passer-by who never noticed you answers when you select them. In wake-word mode, an answer from someone else no longer opens the wake window, so a reply to them needs the comms key.

The setup wizard's Talk button step now offers an optional comms key, on a key, a stick button or both. The Comms help page explains the two keys, the channels, strangers and characters, range, and that real Commanders are never on a channel, and its walkthrough covers binding the comms key. The setup guide has a section on it.

You can now talk to the Narrator. Turn on "Break the fourth wall" under Other voices › Narrator and stories (off by default) and, while a story runs, the Narrator joins the Story channel. Its reply to you on comms may speak to you as "you". Its own narration and the story's lines stay in the third person, and with the setting off the Narrator is not on the channel.

The pilot of your targeted ship and the real NPCs heard on comms can now answer. With the Target channel selected, whatever you say goes to the targeted pilot, who answers from a personality D47 writes for them, their ship and their legal status. With the System channel on a real comms NPC, they answer in the voice of their kind, such as a pirate or station control, from the same kind of personality. Neither is sent what the NPC said, and a Commander you target never answers.

An NPC who answers you on comms now speaks in the same voice as their messages, from the same voice provider. When your own fleet carrier answers you on the System channel, it speaks in its tower voice from the Carrier group, not in a stranger's voice. A targeted pilot keeps one voice whether they answer you first or message you first, and their reply is written for the accent of that voice, as a System channel NPC's already was.

A stranger who answers you on comms now keeps one personality for as long as they are in the system. D47 writes two sentences about them on the background model before their first reply, and every later reply follows it. If the model fails or answers badly, the reply is written as before. The personality is dropped when the stranger signs off or you jump.

The panel now shows the selected comms channel on every surface and every tab: the main window, the desktop overlay and both headset panels. It reads, for example, "COMMS CARRIER · TOWER", "COMMS SYSTEM · NO ONE" when the chosen person has left, or "COMMS NO CHANNEL" when none is open. A target you have not scanned shows its ship with "UNSCANNED". It sits at the right end of the title line, and in the mini panel at the top left of the text area. It cannot be pressed.

You can now choose which comms channel your voice goes to, and who on it: Carrier, Target, System, Story, Crew or Characters. Say "channel up", "channel down", "next sub-channel" or "previous sub-channel", or "comms" followed by a channel or a name, such as "comms carrier" or "comms Tower". Comms channel up and down, and sub-channel up and down, each take a key and a stick button under Acting on the game › What D47 may do › Comms channel keys. All four start unbound and work while Elite has the foreground. After each change D47 says the selection, such as "Carrier, Tower." or "Story, no one." The AI cannot change the selection. A channel opens from the game: Carrier while you own a fleet carrier, Target while you have a ship flown by an NPC targeted, System once an NPC has been heard on comms in this system, Story while a story runs, with the cast it has introduced, Crew while a hired pilot is active, and Characters once you have made one. Switching Commander opens the new Commander's channels.

There is now a second push-to-talk, for comms, under Hearing and sound › Voice Input, below Cancel. It takes a key and a stick button, both unbound to begin with, and works like push-to-talk: held in hold, hands-free and wake-word modes, pressed once to start and again to stop in toggle mode. While one of the two keys holds the microphone open, the other does nothing, and pressing it while hands-free listening has the microphone open makes what you are saying a comms capture. The AI cannot change either binding, and the log warns when Elite binds the same key or button.

Your fleet carrier's tower control now answers on comms, in its own voice and with its own conversation, apart from the captain's. Like the captain it knows the carrier's report, gets fainter with distance, and can't be heard beyond 500 light years. The captain's and the tower's briefs also carry the notes you keep on them.

The characters you made now answer on comms, each with their own conversation and their notes as their personality. One who is aboard is heard on the intercom, clear. One who is somewhere else is heard over the radio, fainter the further away they are, and can't be heard beyond 500 light years: "<name> is <N> light years away, beyond comms range." With Galaxy search off the distance is unknown and the radio is clear. Galaxy search's entry in Privacy and egress lists the systems your remote characters are in while there are any.

Asset Mgmt has a Characters page, after Crew. It lists your carrier's captain and tower, the cast the running story has introduced, and the characters you made, each with a picture and personality notes. The captain's and the tower's names and voices are the same settings as before; the notes you keep on them, and on a story's cast, are yours alone. Add a character of your own with Add a character: a name, notes, and whether they are aboard or which system they are in, with how far away that is. The headset panel shows the same page and asks for names, notes and systems on its entry page.

Crew seats are now characters. The first time this version loads, every seat you named becomes a character aboard, with its name and voice and its role in the notes ("Helm."). A name used on several ships becomes one character. Your core speaks every callout a seat used to speak, and the core reads the mission board again. Fleet › Crew lists only the pilots you hired in the game, and the six crew voice rows are gone from Speech settings. The old seat file is kept as `crew-seats.migrated.json`. A character you give a voice on the Characters page now speaks in it, aboard and over the radio, while that voice belongs to the provider speaking for them; otherwise they speak as before. A character's replies show their picture in the Conversation, and one over the radio is shown under their own name rather than as invented. On the Characters page, a refused field's reason is written under the field instead of over it, and a change made just after another one no longer saves the character as it was before the first, which could put back an old value or show a refusal for a save that had worked.

A story's character can now ride in your ship. A story says at which of its beats they come aboard and leave, and they board or leave at the objective where that beat happens in the chapter. Their lines before that objective are heard over comms and from it on in the cockpit, and the reverse when they leave. If no chapter reaches the beat, the first chapter after it opens with the character already aboard, or already gone. A chapter may also bring a character aboard for that chapter alone. While aboard, the character is heard without the comms-link sound their story gives them, and the chapter is written with them there. Clues, reactions and replies follow who was aboard at the last objective you completed. When you answer a story's character, their reply also follows the notes you keep on them on the Characters page.

A story that is published again now reaches an install that already has it. The story list carries a checksum of each story's file, and a copy on disk that does not match it is downloaded again; the old copy stays until the new one and its pictures are complete. Stories published before this keep the copy you have.

A long-haul story no longer sends every chapter across the galaxy. After the beacon scan and before the finale, a chapter may go anywhere only when it holds a discovery, map, footfall or organic activity, or its Goals step is an Exploration or Exobiologist rank. Any other chapter keeps to a session's flying, and so does a chapter you change the objective of.

A story no longer sends you to a Guardian beacon in a ship with no laser or railgun. A ship with neither, with or without a fleet carrier, is not in reach of the beacon, and the chapter works toward fitting one. Every line that sends you there says the beacon's three pylons must be charged with laser or railgun fire first, and that its core releases an Ancient Key needing a cargo scoop or collector limpet and 1 t of free cargo.

A cast member whose description gives a manner of speaking, such as "clipped file notes", no longer opens every line with that manner as a label ("File note: …"). The story writer and the live replies are told the manner is how the character talks, not a set phrase. Lines already saved in an adventure keep their prefix until that adventure is written again.

A story chapter no longer asks you to collect a cargo or on-foot item as a material. Steps that wait on collecting a material name only engineering materials; chapters already saved with another name still load.

A story no longer sends you to a Guardian site on a body that cannot be landed on. Only landable bodies with a Guardian signal are offered as site objectives.

Hovering a segment of a goal's track on the checklist page shows that segment's name: the rank on a rank goal, the rank on the Powerplay track, "Clue N" on a story. The collapsed track has a taller hover area than its 6px drawing.

A hail can be answered without naming the hailer. For 90 seconds after a hail, what you say goes to the hailer, until you name your core, say the wake phrase or say goodbye; a command the ship runs without the model still goes to the ship. A passer-by is still answered only by name, and the name can now follow a word such as "oh", "hey" or "thanks": "Hey Fenn, thanks" reaches Fenn. The transcriber is given the names of the people you can answer, so an invented name is less often misheard.

Material callouts no longer repeat during collection-limpet work. "First" is said only when you held none of the material before the pickup, each milestone (25, 50, 75 percent and full) is said only when a pickup crosses it from what you already held, and the running count above 75 percent is gone.

The ship's AI can now read your rank and rank progress in every career and in the Imperial and Federal navies, with the name of the next rank, and no longer asks you to read the percentage out. "What's my rank" and "what are my ranks" are answered without the model. It does not depend on keeping a rank goal.

The Ships goal now counts every hull you own. It counted only hulls the journal names without a localised name, so a large fleet read as a handful. Asking about an owned ship you have never boarded, by hull type, now finds it too.

A stock story can now be a Goals story. Each of its chapters after the beacon scan works the Goal you are furthest behind on, as a share of the whole goal, so your Goals rise together. Near a promotion, the chapter asks for the rank. Further from it, the chapter's activities are ones that earn that rank. Powerplay chapters ask for a pledge or the next Powerplay rank, Engineers chapters for the next engineer on your route, and Ships chapters for the cheapest hull you can afford and do not own.

A Goals story's clues now follow your Goals progress: each waits for its day and for your overall Goals progress to reach a threshold. The thresholds are spaced evenly between your progress when you picked the story and 100%. The finale waits until every Goal is done, Powerplay included. While you are not pledged to a Power, the next Goals chapter asks you to pledge. The story's entry on the Goals page says what the next clue or the finale is waiting for, such as "Clue 5 waits for Goals progress of 60%; you are at 37%". The Story Bench cannot jump a Goals story.

A story or adventure rank objective can now name the Imperial Navy or the Federal Navy, up to King or Admiral, and reads "reach Baron in the Imperial Navy". The story writer is told your navy ranks by name.

A story or adventure rank objective can now ask for Elite I to Elite V in Combat, Trade, Exploration, Mercenary and Exobiology, and reads "reach Elite III in Trade". CQC objectives still stop at Elite.

Privacy and egress settings now have a Usage counts switch, on by default, and the egress list names it. It counts how often each tool is used and sends each finished day once, at startup and hourly, with no identifier. Turning it off empties the counts and deletes data\usage-counts.json. A test drive counts but never sends.

The privacy, data retention and donation privacy pages no longer say d47 has no analytics. They now say d47 sends one thing without being asked: once a day, how many times each tool was used, with no identifier and nothing from your journal. It is on by default, and Usage counts in Privacy settings turns it off.

The Carrier page now has a DRAKE / FORTUNE / NAUTILUS / VICTORY choice above the carrier's picture, shown when Hull pictures is on. The game does not say which model a carrier is, so you pick it; the choice is kept for that carrier's callsign and the picture changes to match. A carrier you have not picked for is drawn as the Drake.

While D47 is working on a remark in TALK THROUGH THIS BUILD, the reply header shows a "Working" label with three dots lit in turn, as the Stories tab does, in place of the grey "working" text. The dots stop when the answer or the failure notice is drawn.

While the game is not running, the Ships tab and the spoken fleet answer now list the ship you last flew, with the flying badge. Its page reads "You were flying it when you last logged out." The spoken answer says "Last flown" instead of "Currently flying". A ship since swapped into storage is not shown this way, and the first login replaces it with the live ship.

Each bind row in Settings now draws a key slot and a controller button slot, 180 pixels wide each, then BIND and CLEAR, on one line that no longer wraps. An empty slot reads UNBOUND. A row with no button slot, or with no controller attached, keeps the button slot's space so BIND and CLEAR line up with the other rows. While BIND is listening, the slots that are listening read PRESS A KEY… and OR A BUTTON… in cyan. Each group of bind rows has a KEY / CONTROLLER BUTTON header over its first row.

Each of the nine hotkeys on the Window, Overlay and Headset settings pages can now take a stick or throttle button as well as a key, as Push-to-talk and Cancel already could. Press BIND and press the button; giving it the same kind twice replaces that one, and CLEAR removes both. Open settings, Focus the ask box and Find answer the button only while the D47 window has focus, as their keys do. The overlay and headset buttons work while Elite has the foreground.

A key or stick button that another action already holds is now refused. Binding it in Settings or the setup wizard says, for example, "Ctrl+L is already bound to Focus the ask box. Press another, or Esc." and keeps listening; asking by voice or text says to clear it there first. The other action keeps its binding.

Esc now ends a bind that listens only for a stick button, such as the push-to-talk button step in the setup wizard, and leaves the existing binding as it was. It also ends the capture after D47 has declined a button press.

The Chatterbox voice and Local voice rows under Providers › Speech synthesis now show only when that provider is the Voice provider, or when another voice slot or a downloaded story uses it and its model is not yet downloaded. Once the model is installed they go away.

d47 now writes one log file a day, the readable `.log`, instead of that and a `.jsonl` copy. The `.jsonl` files left in `data\logs` by earlier versions are deleted the first time this version starts. Log lines now carry milliseconds, as in `[14:36:09.412 INF]`, and incident excerpts read both the old and the new form.

A new Advanced setting, Model for complex tasks, under Provider and model, chooses the model that talks through a ship's build. On a custom endpoint it is the only way to name one. Changing provider or endpoint clears it.

Talking through a ship's build now runs on the catalog's high model, Claude Opus 5.5 on Anthropic and GPT-6 Astra on OpenAI, at High effort, or on your conversation model with a custom endpoint. Each remark costs more than before.

The Ships page under Asset Management now follows the fleet while it is open. Ships the journal stores, buys or sells appear or disappear without leaving the tab, and the cards no longer read "not bought yet" for owned hulls when the journals finish loading after the window opens.

On the five Search pages in the main window, you can now drag the boundary between the form and the answer to make the form wider or narrower. The width is remembered, and the same width applies to all five pages. The headset keeps its fixed layout.

On the search pages, the saved and quick searches are now a START FROM dropdown at the top of the form, in the window and in the headset. Picking one fills the form and runs it. The button shows the search you picked until you change a field, press CLEAR, or another search arrives. While one of your own searches is shown, FORGET THIS SEARCH sits under the dropdown; the old ✕ is gone. The footer says the phrase for the search shown.

D47 warns when Thargoids pull the ship out of a hyperspace jump, with the interdiction alarm. On a plotted route it says "Hyperdiction. Thargoids are pulling us out of the jump." partway through the tunnel. With no route plotted it says on arrival which system you were pulled out in and which one you were jumping to. The Danger row under Callouts turns it off.

In the Stations and Bodies answer tables, a system name is now cyan when it is the system you are in and the accent colour otherwise, as in the Outfitting, Signals and Systems tables. It was white.

The distance field on the Stations, Outfitting, Bodies and Signals search pages is now the full 220 px wide and shows a thousands separator, so 1000 reads "1,000". On the Stations and Bodies pages it has its own row, with SHOW UP TO below it.

A body search for more than 1,000 light years is searched at 1,000, and the answer ends "I searched 1,000 light years, the furthest a body search goes." Beyond that radius the search takes too long to answer.

A galaxy search with exactly one result now says "1 station", "1 body", "1 signal" or "1 system" in the answer heading instead of the plural.

A galaxy search whose match count reaches Spansh's limit of ten thousand now says "10,000 or more" instead of "10000", since the true number is higher. Station, body, "sell" and system answers all do this.

The distance on the Stations, Outfitting and Signals search pages, and `max_distance` in the station and outfitting searches, now goes up to 99,999 light years. The limit was 500 for stations and outfitting and 1,000 on the Signals page. A radius larger than that is now sent as asked.

Each Commander keeps their own Raven Colonial key. Set it while that Commander is shown; a key stored before this release moves to the Commander it belongs to the first time that Commander is shown. A key entered under the wrong Commander sends nothing.

A core you wrote yourself now has a picture beside its lines in the conversation, the custom core's portrait. It had none.

The Carrier Captain's portraits are new. Both captains wear a burgundy and black command tunic on a carrier bridge, in place of a naval cap and braid.

The last step of setup has a link, THE D47 DISCORD, that opens the community page in your browser.

The bar under a story write now estimates the time at the effort the app writes stories at. For Claude Sonnet 5.5 it expects about two and a half minutes instead of under half a minute; for GPT-6 Luna, about a minute.

A story that opens with a trip to a Guardian beacon puts COVAS aboard until the beacon is scanned, even when a core you made is chosen. Your core comes back after the scan.

Hearing a voice's free sample under ElevenLabs no longer fails when ElevenLabs sends the sample as a WAV file, as it does for Zain, or as an MP3 that changes sample rate partway through. A sample that still cannot be played is now written to the log.

F1 opens help for the page you are on, the same page the HELP button opens. F1 again closes help and shows the page you were on. F1 cannot be rebound.

? opens a list of every key the window answers to, grouped, with what each does and the key bound to it now. It opens when no text box has the cursor, so typing ? in the ask box or the page search box still types a question mark. Keys you can rebind are shown in orange with the Settings place that changes them, and a key with nothing bound reads Unbound. ? or Esc closes the list, and CHANGE KEYS closes it and opens Settings at Window.

Once every 14 days, at a quiet moment, the core says one line pointing at the D47 Discord, and "D47 Discord" in the transcript is a link. The first mention comes 14 days after the first launch. It is said at least 10 minutes into a session, after 90 seconds docked, landed or in supercruise, and at most once per launch, whichever core is aboard. The row "Mention the Discord every two weeks" under Callouts turns it off.

On a page of rows, Down and Up focus the first and last row, then step to the row below or above. In a grid of tiles they step down a column, and Left and Right move along a line. Enter or Space opens the focused row, which draws in Tile2. Down from the page search box focuses the first row the search left. On the Checklist, Stories, Adventures and Messages pages, and on the Materials tiles and rows and the Statistics rows, rows and cards can now take focus. Going back with Escape, the mouse Back button or Alt+Left puts focus back on the row you opened, or on the row now in its place if the list changed; a row opened by a click is not refocused.

A selected row that has keyboard focus draws a 2 pixel Knock frame just inside its edge, so you can tell where focus is on the open arc and the selected item on the Checklist. A row you clicked, an unselected row and a disabled row draw no frame.

A story call on a cloud provider is now stopped by the size of the reply it asks for, not after a flat two minutes. The spine gets 200 seconds and each later step 400 seconds, so a high-effort Sonnet chapter that thinks for two minutes before writing its beats is no longer stopped with "Nothing came back (TimedOut)". A call with no output for 30 seconds, or no first output in 90, is still stopped.

Digit keys print as the digit and numpad digits as "Num 0" to "Num 9", so the headset reset-zoom key reads Ctrl+Alt+0 in Settings and the logs instead of Ctrl+Alt+D0. Stored settings are unchanged.

Ctrl+1 to Ctrl+7 show the first to seventh tab on the bar; the number of the tab you are on goes to the top of it. Ctrl+Tab and Ctrl+Shift+Tab step to the next and previous tab, and Alt+Left goes back, the same as Escape.

On the Progress page and a plan's result page, system names on the route map are letter-spaced as on the Galaxy map.

On the Navigation tab's Galaxy map, the map takes keyboard focus when the page opens and when you click it. The arrow keys pan a quarter of the view, + and − zoom a step, and Home frames HERE. Ctrl with + or − still zooms the window.

On the Transcript tab, Ctrl+Shift+C copies the whole page, the same as the Copy button. Ctrl+C still copies only a selection.

The spend window shows one table instead of Running totals and By provider. Today, This week, Last 7 days, This month and Last 30 days are its columns; the first row is the total, and each provider that charged something has a row with its models under it, highest Last 30 days first. A voice provider shows its characters under each figure. A model with no rate shows "no rate" and makes its window's total start with "≥". Providers that charged nothing, such as Edge Neural and Chatterbox, are no longer listed in this window. The session line reads "N turns · M chars", counting only voices that charge.

The "Cores by ship" row on Your core is gone; the Ship dropdown already shows each ship's core. Lines in ship-cores.json that D47 refuses are no longer reported in the app, and a ship with a refused line keeps whoever is aboard.

The Core help page now opens by saying what a core is: the character that answers you, in its three kinds (COVAS, the Guardian cores and custom cores).

On Your core › Core and character, the "Cores of your own" row is now "Custom cores (N)", where N is how many cores you wrote. The list of names is gone. The button reads MANAGE once you have written one, and WRITE A CORE until then. The count updates when you close the editor or when personas.json changes.

On Your core › Core and character, the Core humor, NPC humor and Carrier crew humor bars can be pressed and dragged like the Guardian effect bars. Pressing a segment sets that level. The stepper still works, and 0 is set with the stepper's left arrow or the Left key.

On Your core › Its voice, the COVAS reverb checkbox, the line "Hear the COVAS reverb without paying for it." and the TEST button are now on one line under COVAS Voice. Before, TEST sat on a row of its own, labelled "Test", under the checkbox.

The help under the Voice group on Your core › Its voice, the help under the Provider group on Providers › Speech synthesis, and the help on the Voice row now link to each other. Pressing "Providers › Speech synthesis" or "Your core › Its voice" opens that page. The Voice group's help also says that Speaking rate is shown only where the speech provider can change the pace.

D47 reads your sticks, throttle and pedals through Windows HID, and stores each controller interface under its USB device name. Before, Windows gave the same stick a different id in each copy of D47, so a switch bound in one copy did not work in another. A binding also keeps working when you move the controller to another USB port.

Bindings you made before this update carry over on the first start: switches, push-to-talk and cancel move to the new identity when D47 can tell which controller each one was. Where it cannot, flip one of the switches on the HOTAS switches page, as before.

The first group on the Mining and materials settings page is now titled "Mining and material callouts", since it also holds the material callouts setting.

When the galaxy search fails while a story chapter is being checked, D47 waits a few seconds and asks again. If it fails a second time, the write stops with "The galaxy search is unavailable, so the chapter's places cannot be checked" and the search's own message, and no further draft is started. Before, the failure counted against the chapter as a refused objective and used up a repair round. Story chapters written with Claude Sonnet 5.5 also have more room to think, so a chapter no longer fails with "The model did not write the objectives" after about two minutes.

A story's premise, want, stake, turn and ending no longer have a length limit. Before, a premise one character over 700 failed a draft that had passed every other check, and the next draft failed the same way.

The Callouts setting formerly labelled "Trading Mode needs a hold of (tonnes)" is now labelled "Trading Mode requires a cargo capacity of at least (tonnes)". Only the label changed; the value, its default of 25 and the saved setting are the same.

Button labels are centred. Before, a label sat at the left of any button wider than its text, such as CLOSE at the foot of the spend dialog and other dialogs.

A box that opens over the window with a text field, such as Find (Ctrl+Shift+F) and the name box when you save a search, now opens with the cursor in the field, so you can type at once. Before, you had to click the field first.

Story chapters are written again. The check that reads a chapter's lines against its objectives now checks only what you did, where you have been and how a game event went. The story's cast, what they say, send or own, records in the story and events before it began are not checked, and the ending may recount what earlier objectives confirmed. In a stock story, the Guardian cores coming aboard at the beacon is no longer refused. An objective the check has passed is not refused by a later round, and a refused statement is deleted rather than reworded. Stories written with Claude Sonnet 5.5 use high effort, so they take longer and cost more. A chapter this check still refuses says "The story states what its objectives do not confirm" rather than "The story names places that cannot stand".

When two requests to the same model provider start at once and both are refused for a setting the model does not take, both are now retried without it. Before, only the first was, and the second failed with the provider's "Unsupported parameter" message.

The Inara API key setting is gone, and so is the request to Inara. Community goals come only from your journal: the board at each station you have docked at, and the answer says so. A key stored earlier is deleted the next time D47 starts, and Community goals no longer appears under Privacy and egress.

Construction answers and the Construction page no longer name the planning service. Answers say "the colonisation planner", and the page's planner button reads PLANNER. The settings and the tooltips that open the website keep the name.

The Search tab and the star system page no longer name the data service. Messages read "the galaxy search" or drop the source: "GALAXY SEARCH UNAVAILABLE", "Asking the galaxy search", "Last reported". The failure notice shows the time without the host name.

Spoken answers no longer name the data service. The arrival callout says "Commanders have surveyed biology here", and unknown systems are "No system is named X" or "There is no record of X". Earlier biology finds and first-in-region callouts say "No Commander has reported X" or "Commanders have logged N earlier finds" instead of naming the service. The "Earlier biology finds from Canonn" setting keeps its label.

The session-length reminder now says to take a break: "You have been flying 4 hours, Commander. Time to take a break." The switch in Settings > Callouts is now called Break reminder. Its setting and hours choice are unchanged.

A ship type that starts with a vowel now reads "an" in the recap, the logbook and NPC chatter: "an Anaconda Mk II", "an Imperial Clipper". A missing ship type reads "a ship".

GalNet headlines at launch wait for the opening lines. The order is the greeting, the recap, then GalNet; headlines that arrive earlier are held and said right after the recap. With the recap off they follow the greeting, and with both off they are not held. A login by another Commander does not hold them again.

The opening greeting's second sentence varies between launches. It is one of ten, such as "Standing by." or "Let's fly.", instead of always "Ready to go.". With a persona on, the core keeps the picked sentence unless something real is worth naming.

The transcript no longer opens with "Language model: ready." The "Language model: unavailable" line, with its reason, still appears when no model is available.

The Navigation tab has a Galaxy map page, second after Plan, in the window and on the big panel. It draws Elite's 42 regions with their borders and names, every jump you have made as a line with a dot per system visited, the route Elite plotted (grey behind you, orange ahead, with the start, destination and star marks once the view is 6,400 ly across or closer) and your position. GALAXY, HERE and ROUTE frame the view; plus, minus and four arrows zoom and pan; 30 DAYS, 1 YEAR and ALL choose how far back the travels go; and TRAVELS, ROUTE and REGIONS switch each layer. On the desktop the mouse wheel zooms and a drag pans as well. Under the map are your system and region, the jumps and systems counted in the chosen range, and a key to what is drawn. On the big panel the system, region and counts sit beside the map and the key under the switches. While earlier journals are still being read, this session's jumps are drawn and the page says so. While another Commander is shown, their travels are drawn without a position or route, and HERE and ROUTE are greyed.

Tap a visited system or a route stop on the Galaxy map, in the window or on the big panel, and a strip names it: the system with a copy glyph for pasting into Elite's galaxy map, how far it is from you, and how many times you have arrived and the last date, or "not visited" for a route stop you have not been to. Brackets mark the selected star on the map and its name is drawn beside them. CENTRE HERE recentres the map on it at the same scale, BOOKMARK adds a bookmark named after the system and says so, or says it is already bookmarked, and ✕ or a tap on empty space clears the selection. The selection stays through panning, zooming, a new view and the layer switches. The strip takes the place of the line and legend under the map; on the big panel it covers the map's bottom edge.

The Galaxy map marks the things you keep. Four more switches, ENGINEERS, BOOKMARKS, CARRIER and SHIPS, turn on a white cross for each engineer's system (drawn at 6,400 ly across and closer), a cyan ribbon for a bookmark, a yellow carrier and a yellow chevron for stored ships, with a count beside it from two ships. A bookmark, the carrier or a stored ship in a system you have not visited cannot be placed; the page says how many, for example "2 bookmarks, your carrier and 1 stored ship are in systems you have not visited.". Several kinds in one system are one row of marks. Tapping any mark selects its system, and the strip's second line names what is there: the engineer, the bookmarks, your carrier and the ships by name or type. A bookmark made from the strip appears on the map at once. Another Commander's carrier is not drawn and its switch is greyed. On the big panel the key is now a band over the bottom of the map.

With galaxy search on, the Galaxy map draws the stars around you that you have not visited, as small hollow grey squares, once the view is 6,400 ly across or closer. They are the hundred systems nearest the system you are in, asked for when the page opens and when you arrive somewhere while it is open, and kept for the session, so stars around systems you passed through stay drawn. A new UNVISITED switch, last of the eight, turns them off and stops the search. Tap one and the strip gives its distance and says "not visited". Opening the page also looks up where your bookmarks, carrier and stored ships are when they sit in systems you have never visited, up to 25 systems each time, and draws them. The line under the map says which items galaxy search does not know and which are still to be looked up. With galaxy search off nothing is sent, UNVISITED is greyed, and the line says that galaxy search would draw them. While another Commander is shown no nearby search is sent. The Privacy and egress section names what the page sends.

Ctrl+Shift+F opens Find, a box at the top of the window that searches every page, settings place, setting and help page at once. Results appear as you type, best first and at most eight, each with its path and whether it is a page, place, setting or help page. Up and Down move the selection, Enter or a click opens it, and Esc or Ctrl+Shift+F closes the box. Ctrl+F still searches the page you are on. The key is a row in Settings under Window › Look and keys.

You can ask where a setting or page is: "where is the fuel setting", "where do I turn off hull pictures", "show me the ships page". The window and the headset's big panel both open it, scroll a setting into view and mark it, and the transcript gives its path. When places match equally well, nothing moves and the answer names up to three and asks which. A phrase that finds nothing, or does not name a setting or page, goes on to the model as before.

A link to a setting, from a message or the error banner, now scrolls the row into view and marks it with the hover tile colour for two seconds, or until you press or scroll. A link to a row that lives on another tab, such as Hull pictures on Asset Mgmt › Ships, opens that tab and its settings strip and marks the row there. The same landing works on the headset panel.

A stock story chapter now sometimes ends on a step D47 draws from the events it can watch: a journal event with at most one value to match, or a tool you run by saying its first command phrase. The story's writer is told only that one step and writes its task. Refusing a step keeps that event, or that tool, out of the story from then on.

An open goal is drawn on the Slab ground, no wider than 900 px, with its figures in a strip with no tiles behind them: Rank, Into this rank, Next rank and Ranks to go. The list of every segment with its share is gone. On the open Powerplay goal the strip reads RANK, TO RANK, MERITS and RANKS TO GO, with the merits into the next rank and the merit total; a pledge that has not given its merits reads "not known yet". Perk ticks sit above the track, 10 px and in the accent where held and 6 px and Grey where still to come, with a key and the chapter ranges under the track. A This chapter close-up has a column for each rank in your chapter: held ranks solid, the next rank filled to its percent in Cyan, and what each rank gives. Held and Still to come stand side by side where there is room, Still to come opens with the current chapter and the next two, and a button opens the rest.

Ship specifications follow the 4.4.2.0 rebalance. The Sidewinder, Eagle, Viper Mk IV and Type-6 have more shields; the Viper Mk IV, Asp Scout and Federal Dropship are faster; the Asp Scout, Dropship, Type-7, Imperial Clipper, Mandalay and Caspian Explorer are lighter; and the Type-6, Type-7, Clipper and Dropship have their new internal slots. The hardpoints and optional internals of the Caspian Explorer, Kestrel Mk II, Lynx Highliner and Corsair are now known, and the Type-11's hardpoints read as sizes.

D47 knows the Anaconda Mk II: its speed, boost, armour, shields, mass, slots, large pad, four crew and ARX early access, its six armours with their mass, price, hull boost and resistances, and the 6D Mk II Cargo Rack's capacity of 96. Its seats are offered the Multipurpose crew. The Corsair, Caspian Explorer, Lynx Highliner and Kestrel Mk II now have a pad size and crew figure, and the Lynx Highliner's armour has its figures. A new Passenger crew, offering Comms, First Officer and Navigation, is offered on the Lynx Highliner, Orca and Beluga Liner; the Corsair is offered the Multipurpose crew and the Caspian Explorer the Exploration crew.

Station and outfitting searches accept the Anaconda Mk II by name.

A saved neutron, Road to Riches or exobiology plan's result page draws the same map as a carrier plan, in the window and on the big panel. The start is drawn first and, on a loop, the return closes it. A plan saved before positions were read, and a trade plan, keep the page as it was.

A carrier route's result page draws the route as a map to the left of its stops, in the window and on the big panel. Reached stops are drawn behind you, the stops where the tank and hold run out of tritium get a Blue ringed mark, keyed OUT OF TRITIUM, and a route whose stops have no positions keeps the page as it was.

The Progress page draws the route Elite plotted as a map to the left of the hop list, in the window and on the big panel. A grey line runs behind you and an orange one ahead, with squares at the start and the destination, your own system in Cyan, and marks on neutron stars, white dwarfs, black holes and stars that cannot be scooped, with a key under the map. Off the route, your position is drawn when the journal has one. With no route, fewer than two positioned hops, or another Commander showing, the page has no map and keeps its full-width list.

Each navy on the Standing page lists what its ranks unlock: ships and the Sol and Achenar permits already unlocked with the rank that gave them, the next rank that unlocks something and how many ranks away it is, and everything later.

The open Powerplay goal lists what your Power's ranks give. Held at your rank shows each perk and rebuy cut at its latest value and the rank it last changed at, the modules unlocked (or the first one and its rank), your mini care packages and the rank decal. Still to come lists, chapter by chapter, each rank that adds or improves a perk, rebuy cut or module, as old → new, with the chapter's merits and share of the climb. Small ticks under the track mark those ranks. The voice answer for Powerplay ranks is worded as before.

A goal on the Goals page is drawn as one segmented track instead of two bars: one segment per rank on a career or navy ladder, per Powerplay stretch (sized by the merits it takes) and per line of a story, grouped into chapters labelled with their share of the goal. Passed segments are solid, the current one is filled to its percent with a Cyan marker, and its name and percent sit under the track. The goal's percent is at the right of its name. A Powerplay goal's percent is now its merits over 775,000.

An open goal fills only its head with the accent. Below it, on a darker tile, are the larger track, Rank, Into this rank, Next rank and Ranks to go, and a list of every segment with its share, then the step and buttons. The labels and bars on an open goal can be read; before, they were grey or orange on orange.

Each row of the Discoveries page under a region's Codex has a 44 px square picture at its left: your own picture for the entry, else its stock picture if that is already downloaded, else the placeholder for its family. Drawing the list downloads nothing; a stock picture shows once you have opened the entry's page and it has downloaded. The picture column has no heading and does not sort.

The Discoveries page under a region's Codex is one flat table of every entry the codex table lists for the region, with Category and Sub-category columns and a Status of MISSING or ON RECORD. A sentence above it, "Show MISSING entries in ALL CATEGORIES", filters by status, category and, for a category with several, sub-category. It opens on Missing, so it lists what it did before. Category and Sub-category sort by category and then sub-category, and Clear filters resets the three.

The Short Story and Next chapter forms are redrawn: labelled Reach, Length, Ship and Brief rows with a hint under each, the brief as a tile with a Clear tile beside it, Go under a rule, and a warning with Open settings when asking is shut. The page title is gone.

The protected bar on a settings row now appears only where no voice command can change the row, so the Core row has none. The legend reads "Rows with a bar like this one are protected — these cannot be changed via a voice command, but the others can." and shows only on a page that has a barred row.

Introductions, Cores of your own and Cores by ship no longer print a whole list. Each states a count, names the first three and counts the rest. Introductions names no core, and its Forget introductions button is red and greyed out while none have introduced themselves. The own-cores button reads Write a core with none and Your cores otherwise. Lines `ship-cores.json` refused show as a warning under Cores by ship instead of a sentence in it.

Humor is one scale from 0 (serious) to 10 (comedian). The three level rows draw a ten-segment track between SERIOUS and COMEDIAN, and the model no longer gets a ban list up to 6 or a lift at 7: every level gets its grade, and the speaker's character decides the kind of humor.

The Story Bench page (on while `D47_STORY_BENCH=1`) is redrawn in the design system, with no title of its own under the Stories header. Slots, jump, beats and checkpoints each have a head with a line saying what they do. The active slot has a cyan bar and says ACTIVE where the other slots have SWITCH TO, and its name no longer carries "(active)". Jump shows the story, its length, clues given and stage in four tiles, with labels such as "Clue 4 · day 28". A beat shows ✓ FIRED, WAITING or a dash with its time, and a stirring adventure's NEXT BEAT says it returns once D47 has said the last line. REWIND is a red tile. With no story and no Short Story under way, one message replaces the three empty sections. Slot, beat and checkpoint text, which drew with no colour, is visible again.

Cores by ship now names each bound ship as the Ship dropdown does, from your fleet as it is now. A ship you renamed in game shows its new name without being bound again, and no entry shows a ship id unless nothing else is known about that ship.

The Forget this ship's core button is gone from Cores by ship. To unbind a ship, pick it in the Ship row and choose Nobody for its core.

The Ship name group is gone from Core and Character: Name it answers to and Keep the name when you switch cores. Every core answers to its own name, and a name saved earlier is dropped when the settings load.

The Ship picker under Core and character shows each ship's core beside it, or NO CORE in grey, and marks the ship you are in with "· aboard" in cyan. With no ship picked, the Ship row shows the ship you are in, and the row under it binds that ship. That row now reads "Core for" and the ship's name.

A story objective can now take several steps. Each step has its own place or activity, and the objective's lines are spoken when its last step is done. After a step that is not the last, your core says it was done and what the next step is, for example "Next: collect 3 bounties." Change objective, Mark done and Never ask this act on the step you are on, and Change objective replaces only that step. A chapter can have as many steps as its story needs, and from chapter two on it may have any number of arrive, dock, land or scan objectives. Stories you already have load unchanged.

A story step can now follow one named mission. A step takes a mission of a family, optionally for a faction and at a station, and later steps go to that mission's destination, complete it or abandon it. Only the mission the first step took counts: redirecting it does not complete it, and abandoning it while the story waits for its completion fails the chapter, as does completing it while the story waits for its abandonment. The ship does not yet offer these steps when it writes a story.

A story objective can now begin with a choice of two or three options. The ship or a character offers it in the line before, and D47 then says the phrases to use, for example "Say choose option one to fight, or choose option two to flee." Say "choose option one", "choose option two" or "choose option three"; the phrases work only while a choice is waiting and its offer has been said, and the AI model cannot choose. Then do what the option asks: completing or abandoning a mission an option refers to before you choose fails the chapter, and so does doing what only another option asks for after you choose. An option either rejoins the chapter or ends it in a named ending of its own, and the next chapter is written from that ending. A line that offers a choice and comes due in combat is said in full once the ship is out of danger, and an offer that was never said, because D47 was closed or the story was off, is said again. Change objective and Never ask this are refused on an objective that begins with a choice, and Mark done waits until you have chosen. The ship does not yet offer choices when it writes a story.

While a story choice is waiting, the story page shows each option as a button under Next, and the mini panel shows each as a numbered tile with the phrase to say. In the headset, pointing at a tile or button chooses it, the same as saying "choose option one". On the desktop mini panel, which clicks pass through, the tiles are display only. Once you have chosen, the mini panel shows the option you chose.

You can now ask whether Elite's servers are up. Say "are the servers up", "is elite down" or "server status" and D47 says Online, In maintenance, Offline or Unknown, and how long ago it checked. It answers from the check **Server status** already makes and sends nothing itself; with the setting off it says so and where to turn it on. The AI model can ask too and is given only that word and its age.

A new setting, **Send my colonisation progress to Raven Colonial**, under Sharing in Outside sources, is off by default and greyed out until **Read colonisation plans from Raven Colonial** is on. With it on and a **Raven Colonial key** entered, D47 sends the tonnes you deliver to a construction site, what the depot says it still needs, and when it completes, to that site's project on the planner. Only sites that already have a project there are sent, and only what happens while D47 is running; nothing from older journals. D47 asks the planner whose key it is and sends only while that Commander is the one shown, so an alt's deliveries are not sent. The line under the switch says who it is sending as, or why it is not. Raven Colonial asks that only one app sends your progress, so leave it off if another app already does. The model cannot turn it on, and **Privacy and egress** lists Colonisation progress.

A new **Server status** setting under Outside sources, off by default, asks Frontier's server status page (ed-server-status.orerve.net) whether Elite's servers are up, when D47 starts and then every ten minutes while they are, or every minute while they are not, whether or not Elite is running. Nothing about you goes with the request. The title bar shows **SERVERS ONLINE**, **MAINTENANCE**, **OFFLINE** or **UNKNOWN** left of HELP, and your core says "Elite's servers are down for maintenance.", "Elite's servers are offline." or "Elite's servers are back up." when the state changes, even with Elite closed. A new **Server status** callout setting turns the line off. **Privacy and egress** lists Server status.

You can now ask about the GalNet articles kept on this PC. Say "what's on galnet", "galnet headlines" or "any galnet news" for the titles and in-game dates of the three newest. Say "read the latest galnet article", "read galnet" or "read the galnet article" and D47 reads the newest in full. The AI model can also search the articles by word, title matches ranking above body matches, and read any article by title; it is sent the full text of the three best matches and the titles of up to seven more, only when it asks. These work with the **GalNet news** setting off, from what is already kept. With nothing kept, D47 says so and, if the setting is off, where to turn it on.

Your core now says the headlines of new GalNet articles as they arrive: "GalNet: Faulcon DeLacy Announces Anaconda Mk II.", or for several, "New on GalNet: …" with the three newest titles and, past three, a count. It never reads the article itself. It needs the **GalNet news** setting under Outside sources, and a new **GalNet headlines** setting turns the callout off. Articles that arrive while it is off are not said later.

A new **GalNet news** setting under Outside sources, off by default, keeps a copy of GalNet on this PC. With it on, D47 asks Frontier's GalNet server (cms.zaonce.net) for the newest articles at startup and every hour while it runs, and the first time it reads back up to two years. Nothing about you goes with the request. The articles are kept in `data\galnet.json` and never deleted; turning the setting off stops the requests and keeps what is there. **Privacy and egress** lists GalNet news. Nothing reads the articles yet.

You can now ask "who's online", "who is online", "which friends are online" or "any friends online". D47 names up to three friends Elite has reported online since it started, newest report first, and counts the rest, for example "Reported online since Elite started: Arcturus, Vega, Rigel and four others." Elite does not write a line for most friends who leave, so this is who has been reported, not who is online now. With Elite closed it says so. Answered on this machine; the AI model can ask too, which sends the names to your language model.

Your core now says when a friend comes online: "Arcturus is online.", or for several, "Arcturus, Vega, Rigel and two others are online.", with at most three names. A burst of arrivals within five seconds is one line. The restatement Elite writes after login stays quiet, and a friend is said again only if their last report is over thirty minutes old. Going offline is not said, because Elite does not write it for most friends who leave. The names are spoken by your ship's voice provider. A new **Friends online** setting turns it off.

Your core now remarks when you hold the ship in a mode for a while: silent running, flight assist off, hardpoints out with nothing hostile around, the cargo scoop left open, the landing gear down away from a station, or mass locked in danger. It speaks once per mode per game session and at most one remark every five minutes, never while docked, landed or in supercruise. With personality on the core says it in its own voice; with it off, the line is said as written. A new **Ship modes** setting turns it off.

Your core now remarks when a notable ship hails nearby: a wedding or funeral convoy, nomads, a refugee flotilla fleeing a war, a tour liner, a protester, a propagandist or a security academy training flight. It speaks once per kind per game session, whether or not NPC messages are read aloud. With personality on the core says it in its own voice; with it off, the line is said as written. The remark carries only the kind of ship, never its name or what it said. A new **Notable ships** setting turns it off.

Each screenshot you take in Elite is now copied as a JPEG into `Frontier Developments\Elite Dangerous\D47` under Pictures, named with the local time, the system and the body, such as `2026-10-12 19-40-03 Shinrarta Dezhra.jpg`. A second screenshot in the same second and place adds ` (2)`. D47 waits up to two minutes for a high-resolution screenshot to finish writing, and skips one that does not arrive. Elite's BMP is left where it is, unchanged. Screenshots taken before D47 started are not copied.

There is a new **Convert Elite's screenshots** setting, on by default, with a **Saved to** line showing the D47 folder inside Elite's pictures folder where the JPEG copies go. Saying "stop converting my screenshots" or "start converting my screenshots" changes it; the AI model cannot. **Privacy and egress** lists **Your screenshots**: the copies stay on this PC, and D47 says that a synced Pictures folder, as OneDrive can be, uploads them.

The journal pane now says where a screenshot was taken: "Screenshot taken at A 1, Example Sector AB-C d1-23", or "Screenshot taken in <system>" when the event names no body. A screenshot with no place still reads "Screenshot taken".

You can now ask whether you have met a Commander before. Say "have I met this commander" and D47 lists your encounters with the Commander you targeted most recently this session, newest first: when, the system, their ship where known, and what happened; with no target this session it lists the ten Commanders you met most recently. "Have I met Arden Vale before" does the same for a name, at most ten encounters with the total. It reads your journals only, never includes message text, and says when older journals are still being read. The AI model can ask too; doing so sends that Commander's name and your encounters with them, and **Privacy and egress** now says so. "Have I met this commander" is answered on this machine.

When you target a Commander you met in an earlier game session, your core now says so: who, how long ago, where, and the most notable thing that happened, for example "Commander Arden Vale — you met three weeks ago in Shinrarta Dezhra; they assaulted you." It speaks once per Commander per game session, not for a first meeting, not while you are under threat, and not until the journal history has been read after a start. The **Commanders met before** callout switch turns it off. The name is spoken by your ship's voice provider, and **Privacy and egress** now says so.

A Commander's log now quotes your captain's log entries. Entries whose time falls inside the log's range are added word for word under **In your own words**, after D47's text and before **Sources**, oldest first, each with its time and system. A log with no entries in range has no such section. The entries are added to the file only: they are never sent to the AI model, the price quoted for the log is unchanged, and no sentence in D47's text cites them.

You can now export your log entries. Say "export my log entries" and every entry for the Commander flying is written, oldest first, to `data\captains-log\<Frontier id>\captains-log-<date>.md`, each as a heading with its date in the game's calendar, a line with the system, body, station, ship and position where known, and the text. A second export the same day is written as `-2` rather than over the first, and D47 says how many entries and where the file is. The AI model cannot export them.

You can now have your log entries read back by voice. Say "read my last log entry"; "read today's", "yesterday's", "this week's" or "this session's log entries"; "read log entries from Sol" or "in Sol"; or "read log entries about Colonia", where every word has to appear in the entry. Matches are read newest first, at most three, each opened by its date in the game's calendar and its system, and more than three ends with "and four more on the Captain's log page." "How many log entries" gives the count, and "delete my last log entry" deletes the newest and says which one went by its date and first words. Nothing goes to the AI model: it cannot read, count or delete your entries, and is refused if it tries. d47's record of the exchange says "read three captain's log entries" instead of their words.

Saying "captain's log" or "log entry" followed by your words saves them as a log entry, word for word, and D47 answers "Logged.". The entry keeps the time and whatever D47 knows then: the system, body, station, ship and your position on a body. Nothing goes to the AI model, and it costs nothing. Entries are kept on this PC in the data folder, and the Privacy and egress section says which speech recognition and text-to-speech providers hear the words. Saying only "captain's log" answers "Go ahead." and saves your next sentence, word for word, as the entry; say "cancel" or "never mind" instead and D47 answers "Not logged." and saves nothing. If you say nothing for two minutes, the opening lapses and your next sentence is handled as usual. d47's log records a log entry as "captain's log entry" instead of your words, so a donated log does not carry it.

A macro step can now press a key chord, such as Ctrl+V, instead of a ship action. In the macro editor, choose **Key chord** in a step's action list, then pick the modifiers and the key. The chord is sent to Elite while it is in front and you are in the game, and the macro stops if either stops being true. The AI never runs a macro that holds a chord: say its name yourself. Asked for one anyway, the AI is refused and nothing is sent. Macros without chords are unchanged.

A HOTAS switch position can now press a key chord, such as Ctrl+V, in whatever window is in front, whether Elite is running or not. It presses once on each flip into the position, and a momentary stick button counts as a switch, so a stick button can paste into a browser. A new setting, **Let a HOTAS switch press key chords in any window**, is off by default, and the AI cannot turn it on; while it is off, the switch's row says so. On the HOTAS switches page, choose **Key chord** under **Outside Elite** in a position's Action list, then pick the modifiers and the key from two lists; a new chord starts at Ctrl+V, and choosing any other action removes it. It works on the desktop window and the headset's big panel. A chord can also be set by adding a `chord` to the position in `data\switches.json`. Alt+F4 and Win+L are refused. If Windows refuses the keystroke, D47 says so.

An owned ship's page gains a **Copy as SLEF** button beside **Promote**. It puts the ship as it is fitted now on the clipboard, with its modules, engineering and modifiers, for pasting into an outfitting website's import. It uses the loadout live where you are aboard and the remembered one otherwise. A ship whose modules d47 has not seen yet says so on the line under the heading and leaves the clipboard alone. An intended hull has no button. It works on the desktop window and the headset's big panel, and there is no voice phrase or tool for it.

A ship build's page gains an **Import from clipboard** button beside **Promote**. With a SLEF loadout for the same hull on the clipboard, it replaces the build's slot plans with the loadout's. Where the build already plans slots it asks first, giving the counts; **Keep this plan** changes nothing. Text that is not a loadout, or a loadout for another hull, is reported on the line under the heading and changes nothing. It works on the desktop window and the headset's big panel, and there is no voice phrase or tool for it.

On the desktop, the Construction page's site detail gains a **RAVEN COLONIAL** button beside the copy glyph for the site's system. It opens that system on Raven Colonial in your browser. D47 sends nothing when you click it; your browser asks Raven Colonial. The headset's page has no button, since it cannot open a browser.

With colonisation plans on, the button opens the project's own page on Raven Colonial, with its commodities, when the planner gave d47 the project's build id: for a depot the planner matches, and for a site in the Planned group. Otherwise it opens the system as before. The tip says which.

A new setting, **Read colonisation plans from Raven Colonial**, under Outside sources, is off by default. While it is on, d47 may ask Raven Colonial for the plan of a system you are building in, by system name and never by Commander name. The Privacy and egress section lists it as "nothing sent" while it is off. While it is on, d47 reads the plan of every system you have claimed or are building in when the setting is turned on or d47 starts, and again when you dock at a construction site there; the plans are kept until d47 closes and never written to disk.

With that setting on, the Construction page gains a **Planned** group below Building and Not seen since. It lists the planner's sites in the systems you have claimed or built in that you have not docked at, each with its name (or its build type where the planner gives none), build type, body and system, and whether it is planned or building. Select one to see its build type, body and status, and for a building one the planner's needs table. A site you have docked at shows its build type and body when the planner knows it, and its Needs table uses the planner's figure where that is lower than your last docking's, with a line saying that some figures are from Raven Colonial and how long ago they were read. Only the site name, build type and body come from the planner. With the setting off the page is as before.

With that setting on, asking about construction sites now includes the plan. Each site you have docked at gains its planned build type and body, and the planned or building sites you have not docked at are listed with their build type, body and status. Asking what a site still needs gives the lower of the planner's figure and your last docking's for each commodity, both figures where they differ, the tonnes on the project's linked carriers, and how many Commanders and carriers are on it. You can ask about a planned site by its name or build type, such as "what does the coriolis still need", and a site you have not docked at is answered from the planner alone, and the answer says so. Notes, links and other Commanders' names from the planner are never passed on. With the setting off, or Raven Colonial unreachable, the answers come from your journal and say the plan could not be read.

Finding somewhere to colonise now lists the best colony first instead of the nearest. Each system is scored from its bodies — Earth-like and water worlds, landable rocky ground, metallic rings with rich reserves, terraforming candidates, biology, size and how close the bodies sit to arrival — and the answer says why each one ranks where it does. Every answer reminds you to check the build slots and the primary port's site in the system map before claiming, since no index has them.

A new setting, **Share with EDDN**, under Sharing in Outside sources, is off by default. While it is on, opening a station's commodity market sends its prices to EDDN, the relay the galaxy sites read, with a random number made on this PC for your Commander and not your Commander name. Nothing from journals read before D47 started is sent, and the model cannot turn the setting on. Opening a station's outfitting or shipyard also sends the names of the modules and ships it sells. With Share with EDDN on, jumping to a system, docking at a station and scanning bodies also send what the game wrote, with the system's position and without fines, fuel, reputation or position on a planet; a scan from a system D47 has no jump for is not sent. The signals and body counts your scanner finds are sent too, a run of signals found together as one message; mission targets are left out. Codex discoveries, settlements you approach, nav beacon scans and routes you plot go as well; a plotted route is where you intend to go, not something the galaxy shows. The Privacy and egress section lists EDDN as "nothing sent" while it is off.

Say "which way to the nearest earlier find" or "guide me to the nearest earlier find" on a body and D47 gives the distance and bearing to the nearest spot where another Commander logged a biology entry there, such as "The nearest earlier Bacterium Aurasus - Green find is 2.4 kilometres away, bearing 210, south-west." Name a variant, such as "which way to the nearest Aurasus", to limit it to that variant. It uses the finds already fetched from Canonn for the system and sends nothing. Your own samples still answer to a plain "which way to my last sample".

Say "mark this spot", "mark this location" or "mark my position" and D47 saves where you stand on the body as "mark 1", "mark 2" and so on, answering "Marked as mark 1." Marks last until you leave the body and are not kept across a restart. Say "where is my ship", "which way to my last sample" or "which way to my mark", or "which way to" a mark's name, and D47 gives the distance and compass bearing, such as "Your ship is 1.2 kilometres away, bearing 140, south-east." When Elite reports the way you face, it adds where the point lies, such as "It is ahead on your right." Nothing leaves your machine.

The On this body page lists each saved point under the distance block, such as "SHIP · 140° SE · 1,240 M": your ship, each mark and your last sample of each genus being sampled. The rows follow your position, say "No position" when the game gives none, and disappear at liftoff or when you leave the body. The VR panel shows the same rows.

With Earlier biology finds from Canonn on, the On this body page also lists the nearest earlier find of each variant on the body under the saved points, such as "AURASUS GREEN · 210° SW · 2,400 M", nearest first. It shows five and counts the rest, says "No position" when the game gives none, and reads only the finds already fetched, so it sends nothing. The big panel in the headset shows the same rows.

D47 now reads the direction you are facing from Elite's Status.json, in degrees, when Elite writes it. Nothing uses it yet.

A new setting, Codex pictures, controls whether D47 fetches stock codex pictures from a GitHub release. It is on by default. D47 fetches the list when a codex page first wants a picture in a session and each picture when a page first shows it, and keeps them in `data\codex-pictures`. Only the picture's file name goes with the request, which gives its codex entry and region. Off, nothing is fetched and only pictures you saved yourself are used.

Say "take a picture", "take a picture of this" or "save a picture" and D47 saves a full-size picture of Elite's window, or of the headset's left eye, to the captures folder in your data folder, with a note beside it of the system, body, codex region and the codex entries you logged in the last ten minutes. It answers "Picture taken." and names up to three of those entries. It works with Let the model look at the screen off, the model cannot ask for it, and the newest 100 pictures are kept.

On Commander › Ledgers › Codex, the regions table has a Discoveries column showing the entries on your record against the entries known for each region, such as "38 / 746", the same under every period. Under the table, Other regions lists every region the codex table knows that has no entries in the period, with the date you last logged there or "Nothing on record". Open one to see its page and what is left to discover. The sort headings order both lists, and the search filters the other regions by name.

On Commander › Ledgers › Codex, a region's page has a Discoveries row that opens a page listing the codex entries the codex table knows for that region and your record lacks. It shows how many are missing, on record and known, and groups the missing ones by category and subcategory, most reported first. The figures cover your whole record, not the period. The Codex Discoveries answer now ends "Stars, Thargoid sites and planets other than the green gas giants are not covered."

On Commander › Ledgers › Codex, the entries on a region's page and the missing entries on its Discoveries page can now be pressed. Each opens a page for that entry under the region, with its picture drawn square and a line saying where it comes from: "Your picture" or "Stock picture", with " · from" and the region's name when the picture is from another region. The page also shows the entry's category and subcategory and each time you logged it in the region, newest first, with NEW for a first logging, or "Not on your record in this region.". With no picture of the entry from any region, it says "No picture of this entry yet.", or "No picture of this entry. Stock codex pictures are off." with Codex pictures off, and shows a grayscale placeholder for the entry's codex family: stars, gas giant planets, terrestrial planets, geology and anomalies, organic structures or Guardian objects. While a stock picture is fetched the page says "Fetching the picture." and draws it when it arrives. The headset's big panel shows the same page. There is no placeholder for Thargoid objects yet, because no journal on hand shows which name the game gives that family.

A codex entry's page in the main window can now hold a picture of your own. **Choose a picture** lists the pictures you took with "take a picture", newest first and those taken with this entry first. Press one to crop it: drag inside the square to move it, drag a corner to resize it. The square stays inside the picture and is never smaller than 384 pixels of it. **Save** keeps a 768 by 768 picture, the capture it was cut from and the square in `data\codex-pictures\local`. With a picture of your own, the page offers **Re-crop**, **Replace**, and **Use the stock picture** or, with no stock picture for the region, **Remove my picture**; either deletes your picture. The headset's big panel shows the picture and none of these. **Privacy and egress** lists Your captures and codex pictures: kept on this PC, sent nowhere.

For the maintainer of the stock codex pictures, a new advanced setting, **Codex publish token**, holds a GitHub token that can write only to dseelinger/d47-codex-pictures; without one, D47 reads `D47_CODEX_PUBLISH_TOKEN` from the environment. With a token, a codex entry's page in the main window offers **Publish** when your own picture is on show, which adds it to the stock pictures every Commander sees and then shows the stock copy, and **Unpublish** when there is a stock picture, which asks first and then takes it off. One runs at a time and the page says "Publishing…" or "Unpublishing…". A failure shows GitHub's message and changes no file on this PC. Neither button shows without a token or on the big panel. **Privacy and egress** lists Codex picture publishing, active only while a token is present.

The codex answer that says which entries are known in your region and which you have not logged is now called Codex Discoveries. Ask "codex discoveries", "my codex discoveries", "what codex entries am I missing" or "missing codex entries". "Codex checklist" no longer answers, so it is no longer confused with Commander › Checklist.

Ask "what geology is here" or "likely geology" and D47 lists the geological features the body could hold, from its class and volcanism, and says whether each is in your codex for the region you are in. Name a body to ask about another one; otherwise it answers for the latest body you saw with geological signals.

When you map a body with biological signals, D47 can name the variants other Commanders have already logged on it, from Canonn, such as "Canonn has earlier finds on this body: Bacterium Aurasus - Green, Stratum Tectonicas - Lime, Osseus Fractus - Indigo and two more." The variants with the most finds come first, up to three are named, and a body with no finds says nothing. It needs the new setting "Earlier biology finds from Canonn" in the galaxy search settings, which is off by default: with it on, the name of the system you map a biology body in is sent to Canonn, once a session for each system, and never on arrival. The switch for the line is "Earlier biology finds on mapping" in the callout settings.

Ask "what has been found on this body", "what has been found here" or "earlier finds on this body" and D47 lists the variants Canonn has earlier finds of on the body you are at, with the number of finds of each, most first, such as "Canonn has 14 earlier finds on C 3 a: Bacterium Aurasus - Green 5, Osseus Fractus - Indigo 5, Stratum Tectonicas - Lime 4." Name a body to ask about another one. With Earlier biology finds from Canonn off it says so and sends nothing; with it on, asking sends the system name to Canonn, once a session for each system.

When you log a new organic codex entry that the Canonn table in your build has no report of in your region, D47 says so, such as "Canonn has no report of Fonticulua Fluctus - Yellow in the Inner Orion Spur. It may be a first for the region." If the table has no report of it anywhere, the line says it may be a first in the galaxy. It says nothing for an entry you already logged in that region, and waits until your codex history has loaded. Nothing leaves your machine. The switch is "Possible region firsts" in the callout settings.

When you scan a landable body with geological signals, D47 names up to three of its likely geological features that are not in your codex for the region you are in, such as "B 3 could have water ice fumaroles or water ice geysers, not in your codex for this region". It waits until your codex history has loaded and says each body once. The switch is "Geology missing from your codex" in the callout settings.

When you first scan a planet that would pay 500,000 credits or more if you mapped it, D47 names it and says about what mapping it pays, such as "a terraformable high metal content world, about 2.4 million if mapped". Stars and bodies you mapped before scanning are left out. The callout and its credit threshold are in the callout settings.

When you scan a body with a rare trait, D47 says so: a colliding binary, a shepherd moon, a moon of a moon, an eccentric orbit, a fast spin, very wide rings, or five or six of the premium FSD injection materials. Each trait has its own switch, and landable with an atmosphere is off until you turn it on.

A story beat reached in combat no longer loses its hand-off. The beat's line is still not said late, but once the ship is out of danger you hear where to go next.

A story objective can now send you to a Guardian ruin or structure, which it meets when you approach the site, or to a tourist beacon, which it meets when you scan it in the beacon's system. No story asks for either yet.

The story writer is now given the unvisited Guardian sites and tourist beacons within reach, from the galaxy search and the signal index, and may send a chapter to one of them. The signal index is fetched when a chapter is written as well as when you search for a signal, and only while the Signal index setting is on.

When d47 restarts while Elite is running, the startup recap now covers what you have flown since logging in and opens "Earlier this session". Before, it described the session before that one and could name a station you left long ago. If you have only logged in, it still recaps the session before.

A Road to Riches or exobiology loop now counts the jumps back to the start in its total. Before, a loop answered with 1, 2 and 2 jumps was reported as 2 jumps where it takes 4. The Road to Riches answer says "including the jumps back to" the start system instead of "and back". A plan saved earlier keeps its old total until you plot again.

A system name at the start of a body name is spoken as written: "Nobody has set foot on Eotienses ABC 2 c" is no longer read as "on it ABC 2 c".

A body in a populated system is no longer a first footfall, whatever its scan says. Elite writes `WasFootfalled:false` on some of those bodies, and d47 used to announce a first footfall there, credit a first-footfall objective and price an organic analysis at five times. It now does none of the three.

The Guardian beacon list now has all 28 systems that carry a Guardian Beacon signal, up from seven. Scanning the beacon in any of the other 21 now wakes the cores and ends a story's act one, and a story sends you to the beacon nearest you, not the nearest of seven.

A story or short story is now checked before you see it: a second model call reads each objective's lines against what that objective can observe and writes again any objective whose lines claim more, such as a mission line that calls its target clean or an ending that says you broke off a fight. The call goes to the provider that wrote the draft, so no new data leaves your machine.

A story or short story objective can ask for new codex entries, which count only the first time you log an entry in a region, and for Xenological (Civilisations) or Guardian codex entries. The Guardian beacon scan counts toward the Guardian objective.

When you did a story objective and the journal missed it, press Mark done under NEXT on a story chapter's page or a short story's page, or say "mark this objective done", "mark the objective done" or "this objective is done". The button asks first; the phrase does not. The objective fires as if the journal had shown it: its lines are told, and a chapter's last objective moves the story on to the next chapter. The phrase marks the running story's chapter, or with no story running the one short story under way; with two or more short stories it names them and asks you to use the button on the right page. The Guardian beacon scan cannot be marked done. The logbook names a marked objective as marked done by the Commander.

A short story you have begun and not finished now shows Change objective and Never ask this under NEXT on its page, as a story chapter does. Change objective asks what you would rather do, proposes a replacement, and lets you use it, keep the current one or say something else. Never ask this has a different objective written. Each changes only the short story whose page you are on. Both say why they cannot when no language model is configured or galaxy search is off. There is no voice phrase for a short story.

A short story that is under way now offers Remove instead of Abandon on its page. Remove asks first, then deletes the story with its messages and drops any objective still waiting to be announced. A short story already set aside still lists with Begin again and Remove. A full story's Abandon is unchanged.

When a story objective will not complete, the ship AI now knows what you can do and says so instead of coaching the same objective again. In a running story it tells you to say "change this objective" or "never ask this", or press Change objective or Never ask this under NEXT on its page. At a Guardian beacon scan it says only the scan gets you past. For a short story it points you to the buttons on its page. It also knows that only your journal or you complete an objective and that it cannot complete, skip or change one.

Ask "codex checklist", "what codex entries am I missing" or "missing codex entries" and D47 says how many codex entries are known in the region you are in, how many you have logged and how many are missing, by kind, and names the three most-reported missing ones. You can ask about another region by name. An entry logged before your oldest journal reads as missing, and stars and planets are not covered.

A story of 1 month or longer no longer sends you back to a Guardian beacon when an earlier story you picked recorded a beacon scan. It opens with the scan behind you, its core comes aboard once the opening has been said, and D47 says the cores from your last scan are still in the ship's systems. One recorded scan leaves the Heretic waiting for a real scan in any system; two leave nothing held back. A scan made with no story running does not count. The story's page drops "The beacon" and the Pick and Switch text no longer promise a trip while a scan is on record. The Stories page has a checkbox, "Fly to a Guardian beacon again in my next story", that makes the next long story fly to a beacon again; a real scan in that story clears it.

Ask "suggest a home system" and D47 names up to three systems you could settle, each with the reason and its two strongest careers. Systems you have claimed come first; otherwise it uses your squadron's minor faction and your pledged Power where you have them, and your career ratings where you have neither. If you are in a squadron with no minor faction stored, it says so and asks for one. It does not set your home system; ask for that after hearing the suggestion. This needs Galaxy search, and the Privacy and egress section now lists the claimed system names, pledged Power, squadron faction and position it sends.

Say "my squadron's faction is" and a minor faction's name, or "our squadron's minor faction is" and a name, to tell D47 which minor faction your squadron backs. D47 asks the galaxy service whether a faction by that name exists and stores it only if one does; with Galaxy search off it says the check needs Galaxy search. The name is kept for the Commander who is flying and for the squadron they are in, so leaving or changing squadron leaves no faction until you name one for the new squadron. The model cannot set it.

Story chapters and short stories can now ask you to pledge to a PowerPlay Power, and to reach a PowerPlay rank with the Power you are pledged to. A pledge is met by a `PowerplayJoin` event and a rank by a `PowerplayRank` event of at least the asked rank. A pledge is asked only of a Commander known to be unpledged, and a rank only of one who is pledged and below that rank.

Story chapters and short stories can now ask you to unlock one item at a tech broker, such as the Guardian FSD Booster. It is met by the `TechnologyBroker` event for that item, and is asked only for items you are not known to have unlocked. A Guardian item needs a Guardian blueprint and Guardian materials, so a story can send you to gather them first.

Story chapters and short stories can now ask you to deliver tons of cargo to colonisation construction sites, optionally of one commodity, and to claim a system for colonisation. A delivery counts the tons in each `ColonisationContribution`, and a claim is met by a `ColonisationSystemClaim`. A claim costs credits, so the story asks for one only when it gives you a reason to found a colony.

Story chapters and short stories can now ask you to be the first to walk on one to three worlds. A footfall counts when you disembark onto a landable body whose scan said no one had been there, and each body counts once. You will usually have to leave populated space to find one. It is asked only of Commanders with Odyssey.

Story chapters and short stories can now ask you to scan three to eight bodies that no one has discovered. Each body counts once, however many scans Elite writes for it. The discovery tag is awarded when you sell the data, so the story promises the find, not the tag.

Story chapters and short stories can now ask you to boost off a jet cone once or twice, at a neutron star or a white dwarf. The objective counts each boost the journal records, and a `BoostValue` of 4 or more is a neutron star. **Change objective** and **Never ask this** treat it like any other activity.

Short stories and story chapters no longer ask you to change your ship's livery. The journal shows only the paint fitted to a ship, not the paint you own, so d47 could not tell whether you had a paint to change to. A chapter written earlier that has a livery objective still loads and finishes as before.

A Commander who changes the same kind of activity away five times with **Change objective** is no longer asked for it: it is barred from every later story chapter and short story, as **Never ask this** bars it for one story. Courier missions, bounties and the other activity kinds are counted separately for each Commander, and the counts and the bar survive a restart. **Keep the current one** counts nothing, and nothing lifts a bar yet.

A story's comfort-zone chapter, every third one after the beacon scan, now asks for a kind of mission you have never completed: courier, delivery, smuggling, sightseeing and the rest that the ship and the story allow. Only when you have completed every kind does it use the activity your career figures show you have done least. d47 only knows the journals on this PC, so **Never ask this** bars a kind you have already done elsewhere.

A story whose last two chapters you changed with **Change objective**, while you are more than 1,000 light years from both Sol and Colonia, now heads home. Its chapters cover up to the long-haul reach each, and each must end at least one reach nearer the nearer of Sol and Colonia (Sol, within 1,000 light years of Colonia), or within 1,000 light years of it. Once you are within 1,000 light years of it, the story goes on as before. **Never ask this** does not count as a change. This needs Galaxy search to measure the last stop.

A story chapter that starts more than 500 light years from Sol now ends 100 to 300 light years nearer Sol than it started, so the story works you back toward populated space over several chapters. The chapter writer is told your distance from Sol and the range its last stop must fall in, and a draft outside it is written again. This needs Galaxy search to measure the last stop.

A story chapter after the beacon scan is no longer given bounties, bonds, conflicts, selling, data sales, rescues or missions when no station is within its reach of where you are. This needs Galaxy search; with it off, or when it cannot answer, the activities are drawn as before.

The Stories settings have eight new ratings under **What you like doing**: Combat, Trading, Mining, Exploration, Exobiology, Passengers, Search and rescue and Crime. Each is from 1 to 100, starts at 50, and is kept for the Commander who is flying. Say "set my mining rating to 80" or "rate exploration 30" to set one; a number outside 1 to 100 is refused, and the model cannot set them. They weigh which activities a story chapter after the beacon scan is built around. The Crime rating will have no effect until Frontier's Law and Disorder update.

A new **Signal index** setting under Outside sources, off by default, lets D47 download a daily index of the signals other Commanders have scanned and uploaded to EDDN, from this project's releases on github.com. The index is fetched only when you search for a signal and the copy on this PC is missing or more than 20 hours old, and it is kept in `data\signals`, outside the data backups. Nothing about you goes with the request. The Privacy and egress section lists it.

You can now ask where the nearest signal of a kind is, for example "where's the nearest Hazardous RES" or "find the nearest nav beacon within 50 light years". The answer names the nearest one, how far away it is, how long ago it was seen, and how many more are in range. It searches the signals you have scanned and, when the Signal index setting is on, the daily index, and a signal in both appears once with the later date. It works with Galaxy search off. A system you name with "near" is placed from the signals d47 holds, and otherwise from Galaxy search; with that off, d47 says it does not know where the system is. The answer is kept in the search history and can be saved by name.

The Search tab has a Signals page, after Bodies, for finding the nearest signal of a kind or name. Choose a kind from the **Signal** list, which includes Any RES, or type part of a signal's name in **Or any part of its name**; filling one clears the other. Set where to search from and how far, then press RUN SEARCH. The answer lists each signal with its system, its distance in light years, how long ago it was seen, and whether it came from the INDEX or YOUR SCAN. Pressing a row opens that star system, and the glyph beside the system name copies it. A signal search asked by voice opens the page. While the Signal index setting is off and you have no signal scans of your own, the page says so and points at the setting.

The Search tab has an Outfitting page, after Stations, for finding where to buy modules and ships. Type in the **Add a module or ship** field and the matching items are listed under it: "5a fsd sco" and "frame shift drive sco" both offer the Frame Shift Drive (SCO). Choosing one adds it as a tile, and pressing the tile removes it. Once a ship is added the field offers modules only. Set where to search from, how far, the landing pad, the distance from the star, and whether to include surface stations (on by default) and fleet carriers (off by default). **Run search** lists the stations that sell everything you added. Pressing a row opens that system's page, and the copy button beside the system name copies it. Spoken outfitting searches open the page, and saved searches and the history work as on the other pages.

You can now ask where to buy several modules, or modules and a ship, at one station, for example "where can I buy a 5A Frame Shift Drive (SCO) and a 6A Fuel Scoop in one place". A new outfitting search takes a list of items and finds stations that sell every module, and one ship at most. Asking for two ships is refused, because the service cannot search for two together. The answer can leave out surface ports and settlements, and fleet carriers are left out unless asked for. It is kept in the search history and can be saved by name.

Station searches now leave out Stronghold Carriers of Powers you are not pledged to. Spansh lists them as Planetary Outposts, so a search for a module could answer with one you cannot use. A Stronghold Carrier is kept when its system is controlled by your own Power, and an unpledged Commander gets none. When any were left out, the answer says so, for example "Left out 2 Stronghold Carriers of other Powers."

A new table and draw let code choose a story chapter's counted activities from the Commander's career ratings, the ship's modules and the Commander's ranks, instead of leaving the choice to the writer.

Story chapters written after the beacon scan are now built around three activities that d47 draws before the chapter is written. The outline and the objectives are both told the activities and their counts, the objectives may use only those activities besides places and milestones, and a chapter that leaves one out is written again. A chapter before the scan, and a short story, are written as before. If you refuse an objective that held one of the activities, only that activity is replaced.

Short stories and story chapters now ask for a wider range of activities. Before, chapters leaned on courier missions and bounties. The story outline is now told the Commander does the game's activities, not only travel. The activities and mission families are offered in a different order for each story, and every mission family in d47's catalogue is offered instead of eight. Each chapter is told what the last three chapters asked for and to ask for something else. A comfort-zone chapter whose least-done activities are tied no longer always picks bounties.

D47 no longer sometimes crashes at startup with "The calling thread cannot access this object because a different thread owns it". Background work that starts before the window could become the owner of the window's message queue, and the window then failed to open. The main thread now takes that ownership before any background work starts.

A story's "fit out a ship that can reach the beacon" objective is now met when you arrive in the beacon's system, in any ship. Before, only a ship with the jump range the objective was written with counted, so a Commander who flew there in a ship with less range was left on that objective after scanning the beacon. The objective now also says the jump range it asks for, and that range is never more than 28 light years: a farther beacon used to ask for its distance over 20 jumps, 34 light years for one 687 light years away.

A story chapter written after the Guardian beacon scan no longer sends you back to a beacon. The chapter writer is now told plainly that the scan has happened and the cores are aboard, so a chapter that follows one working toward the beacon starts after the scan instead of telling the trip again.

The line under a settings page title that explains protected rows now draws the same orange bar a protected row carries, at the same left edge, instead of a small grey block character that looked nothing like it.

The Commander's log no longer says no model is selected when the model setting is left on the provider's default. It now uses that default, as conversation does.

Working out what a Commander's log would cost is now optional. **Write it** on the Logbook page is always available, and "write the log" works without a quote: with nothing quoted, D47 reads the chosen window and writes it in one step, then says what it cost. A window with nothing in it is still not written. Changing the span or the dates drops an earlier quote, so a quote is never spent on a different window.

A story chapter or short story whose first draft failed after its outline was written now keeps that outline for the second draft, with the same title, and writes only the objectives again. The third draft writes a new outline, so an outline that is itself the problem does not hold every draft. A draft whose outline could not be read still gets a new one.

A story or chapter draft with objectives that cannot stand is now fixed one objective at a time. The objectives that passed the check are kept exactly as written, and only the refused ones are written again, in up to two rounds, so rewriting one objective no longer breaks another that had passed. The log now records every refusal with its draft and round.

A story chapter no longer fails because a line names a speaker the story does not have. The chapter request now lists the story's speaker ids, and a line given to a cast member's full name, first name or last name is matched to that member. A name that fits two members, or nobody, is still refused.

A story draft on a cloud model that is slow to start no longer fails at 30 seconds. A story call now waits up to 90 seconds for its first text or thinking, and the 30-second silence limit applies only after that. A call that sends nothing for 90 seconds is still stopped, and the log says no first reply arrived.

A short story no longer asks you to board a ship its brief does not name. The board objective is now offered to a short story only when its brief names a hull, and the writer must choose the ship from a list of hull names: the brief's hulls for a short story, every hull for a chapter. Before, such a draft was refused and rewritten.

A generated story or stock-story chapter no longer sends you to dock at a fleet carrier, a megaship or a construction site. Carriers and many megaships move, and a construction site is not a finished station, so a story now only docks at fixed, finished stations. A chapter already written that docks at one of these is unchanged; refuse that beat and it is written again.

A story that docks at a named station now finds it in systems with more than twenty fixed stations. D47 asks for the station by name instead of listing the first twenty in the system, so a station past that limit is no longer refused as not being there.

Commander now has a Ledgers sub-tab between This session and Commanders, on the desktop and on the big panel. It lists BGS work, Combat stats, Credits and Codex, each with a one-line description. Press one to open its page; the breadcrumb, such as "Ledgers › Credits", takes you back to the list, and each page can still be opened by name.

Ledgers has a Credits page. It shows where your credits came from and went, from the journals on this PC, for the last 24 hours, 7 days, 30 days or everything: totals for what you earned, what you spent, what moved to or from your carrier, the gaps between the journal and the game's own balance, and your balance, then one row per kind. Press a heading to sort and the panel search narrows by kind or journal event. Press a kind, or ALL TRANSACTIONS, to see the individual transactions with the balance after each; small gaps are folded into one row. While D47 is still reading your older journals the page says so, and if that reading failed a warning gives the time the ledger starts.

Ledgers has a Codex page. It shows the codex entries in the journals on this PC for this trip (since you last sold exploration data), this session or everything: totals for entries, new entries, distinct entries and regions, then one row per region with its three largest subcategories. Press a region to see its entries grouped by subcategory, each with its system, body and position, the time, and whether it was new or already logged and when it was first. Press a heading to sort and the panel search narrows by entry, system or region. While D47 is still reading older journals the page says so, and if that did not finish it says where the record starts.

Ledgers has a Combat stats page. It lists your kills and the credits they were awarded from the journals on this PC, for the last 24 hours, 7 days, 30 days or everything, grouped by enemy ship, your own ship, system or faction. Press a heading to sort, and the panel search narrows it by name. While D47 is still reading your older journals the page says so, and if that reading failed a warning gives the time the counts start at. The big panel's sub-tabs sit closer together.

Ledgers has a BGS work page. It lists the work you have done for minor factions since the BGS tick, or in the last 24 hours when D47 does not know the tick time: one row per system and faction with mission marks, combat, trade, data and, with a tick time, influence before the tick and when you last visited. Press a heading to sort, and the panel search narrows it by system or faction. Without a tick time a notice says so, and on the desktop its OUTSIDE SOURCES tile opens the BGS tick time setting.

Asking for your "codex entries" or "codex ledger" now reports how many codex entries you logged and how many were new to your codex, from every journal on this PC. It covers the time since you last sold exploration data unless you say "this session" or "all", names the three categories with the most new entries and, with more than one region, the three regions, and counts the rest. A region name, or "here", limits it to one region. It waits until all your earlier journals have been read.

Asking "where did my money go", "my earnings" or "credit ledger" now reports the credits you earned and spent, by kind, from every journal on this PC, with your balance. It covers the last week unless you say a day, month or "all", names the three biggest kinds in and out and counts the rest, and "exploration" or another kind gives just that line. Bounties and combat bonds count when redeemed. Credit transfers to your carrier are reported as moved, and a gap of a million credits or more between the journals and the game's balance is named. It waits until all your earlier journals have been read.

Asking "my combat stats" or "my kills" now reports the kills you have made and the credits awarded for them, from every journal on this PC. With no grouping it names the top ship type, system, own ship and faction; "by enemy", "by ship", "by system" or "by faction" names the top three and counts the rest, a period of a day, week or month limits it, and a name narrows it. Every answer says what it covers, such as "on record since 2 July 2025", and it waits until all your earlier journals have been read.

D47 now remarks on combat milestones in the journals on this PC: the 100th, 250th, 500th and 1,000th kill and every 1,000th after, the first of a ship type, and a reward larger than every earlier one once there are ten kills. It says "on record" and speaks one remark per kill, ahead of the existing session remarks. It stays silent about them until all your earlier journals have been read.

Asking "what's my BGS work" now reports the work you have done since the last BGS tick, per system and faction: mission influence marks, bounty and combat bond vouchers, trade sold and its profit, and cartographic and organic data sold. It names the three busiest system and faction pairs and counts the rest, and a faction or system narrows it. With no tick time it covers the last 24 hours and says so. Where your journals hold a reading of the system from before the tick and one from after, each line also gives the faction's influence before the tick and when you were last there.

Web search now works with the OpenAI provider pointed at xAI (`https://api.x.ai/v1`), and the Privacy and egress section names `api.x.ai` as the host that runs the search. Requests to xAI no longer include a reasoning setting, which its Grok models refuse.

The story progress bar and its time hint now use slower times for a model on your own PC: "Usually ready in about 75 seconds" for a short story and 130 for a chapter, instead of 40 seconds for both. Models d47 has measured keep their own times.

Story calls to a local model now send the shape of the answer they expect, so the model fills in every objective's kind instead of leaving it out. The objectives prompt also now lists the conflict and faction kinds in its answer format, for every provider.

On LM Studio, D47 now reloads the story model with 16,384 tokens of context before writing a story or chapter when it is loaded with less, and loads it that way when it is not loaded. The progress line says "Reloading <model> with room for a story." while it does. If the reload fails, D47 says so and writes nothing.

When a story prompt is larger than the model's context, D47 now says so at once, with the size the prompt took and the context to set, instead of writing the chapter three more times and reporting that no draft stood.

When a model's context is too small for a story, D47 now asks for 16,384 tokens instead of 24,576 and says where to set it: LM Studio's context length in the loader, remembered for that model, or the other server's own settings. The conversation help gains a section on writing stories with a model on your own PC.

A local model with a known context now gets a reply limit equal to the room its context has left after the prompt, instead of a quarter of the context, so long story replies are cut less often.

The Story bench lists the beats of the current chapter and of a Short Story under way, with each beat's function, title and whether it has fired. Next beat steps the waiting beat without its journal event, and returns once the line has been said.

Stories written with GPT-6 Luna now ask for low thinking effort instead of medium, which often ran for several minutes and was stopped. The model catalog can name a story effort for any model.

The current story's current chapter page has an Abandon button beside Story on. It asks the same question as the Catalogue card, and on Abandon it ends the story and returns to the Your stories list.

While a short story is written, the Short Story page shows a bar with what D47 is doing ("Writing your objectives…"), the time so far and how long it usually takes, and a Stop button that ends the write, and D47 says nothing. Leaving the page no longer loses the write: the bar is there when you come back, on the desktop and in the VR big panel. When the story is finished D47 saves it and says so straight away, whichever page or tab is showing, and opening Short Story afterwards takes you to the draft.

While a story chapter is written, the Stories tab shows the same bar under the story's standing line, with what D47 is doing, the time so far and how long it usually takes, on the desktop and in the VR big panel. A second or third draft does not move the bar back, and it goes when the chapter is written or the write fails.

While a jump on the Story bench writes a chapter, the bench page shows the same bar under its notice.

Edge Neural speaks again. Microsoft changed how requests to the voice are signed, and every line failed with a 403 error; D47 now signs them the new way.

A new setting, BGS tick time under Settings › Providers › Outside sources, lets D47 read when the last BGS tick happened from tick.infomancer.uk: once when you turn it on or start D47 with it on, then every ten minutes, and only while Elite is running. It is off by default. The request carries nothing about you, and it is unencrypted because the host answers only over plain HTTP; Privacy and egress lists it as BGS tick. D47 keeps the time across restarts.

When a local model's context is too small for a story, D47 now says so, with the token counts and the context length to set on the server, instead of "The chapter could not be written". It does not write the chapter again, because a second try fails the same way.

A short story that cannot be written is now written again, up to three drafts, as a story chapter is, and D47 says before each new draft why the last one was not used. A refusal you can act on, such as no language model being set, still stops after one draft.

A model call for a story chapter or a short story on a cloud provider is now stopped when it is still running after two minutes, or when no text or thinking has come back for thirty seconds, and counts as a failed draft, so the next draft is written. A change to a short story by remark is stopped the same way. A model running on your own PC is never stopped for being slow.

With BGS tick time on, D47 now says when the BGS has ticked, once per tick: "The BGS has ticked." for a tick up to fifteen minutes old, or "The BGS ticked forty minutes ago." up to an hour. An older tick is not said, so a restart stays quiet. Turn it off with the BGS tick row under Callouts.

You can now ask when the last BGS tick was, by voice or typed, with no language model needed: "when was the last tick" answers "The last BGS tick was at 16:14 UTC on 15 October, 23 hours ago." With BGS tick time off it says so and where to turn it on, and with it on but nothing read yet it says it hasn't read the time.

When a DSS names the genera on a landable body worth the high-value biology threshold, D47 now says each genus's candidate species and the value range before you land, once per body: the species itself for one candidate, both for two, "one of three" for three, and "and N more" past three genera. The `Scan` that follows says nothing more for that body.

Asking about a body you have mapped now lists, under each genus the surface scan named, the species its conditions admit, with each one's value, most valuable first. It says that this is a prediction and the sample decides. A species whose surveyed gravity, temperature or pressure range the body misses by under 2% of the range's width is now counted as possible.

A HOTAS switch set to a power distributor split now sets it when you flip it away and straight back. If the pips had not yet moved for the first flip, D47 took the second flip as already done and pressed nothing, so the distributor ended on the split you had flipped away from.

The Catalogue now reads as a sentence: "Show All stories of Any length", with the length, rating and sort added as you choose them. Each word in the accent colour is a button that opens its choices under the sentence, and the level takes any number of ticks, such as New commander and Endgame together. A line under the sentence says how many stories match and offers Clear filters. Stories are cards in a grid, three across on the big panel, each with its first picture large and any others in a column beside it, its length, level and core, the start of its blurb and its rating. The heading and the paragraph above the list are gone; the title line carries the summary, and the sentences about picking, the Guardian beacon and the Guardian cores now sit on each story's page above Pick or Switch. The sort that was called Highest rated reads Highest rating.

The Stories tab now shows its pages as a row of text sub-tabs under the header, like the Transcript tab: Your stories, Catalogue, Short Story, Story log and Story bench. The buttons on the Stories page are gone. Short Story appears only while a language model and galaxy search are on, Story log only while a story is under way or waiting for an answer, and Story bench only while the bench is on. Your stories says why Short Story is missing whenever it is. The Story log sub-tab no longer shows an unread count. A switch position saved on one of these pages, and "switch to catalogue", "switch to story log" and "switch to short story", work as before.

"Switch to" now reaches every core by voice, and works when speech recognition misspells the name. "Switch to COVAS" puts the stock core back, where before it was answered with "Did you mean 'switch to elite', 'switch to warden', or 'switch to cora'?". "Switch to Quora" is Cora, "switch to Kovas" is COVAS, "switch to Llama" is LLaMo, "switch to Mentor" is Mender and "switch to the cartographer" is Chart, all without the model. A word that only sounds like a core, such as "switch to carrier", asks "Did you mean 'Cora'?" first, and a name that sounds equally like two cores offers just those two. "Switch to Quora please" works too. "Switch to" also reaches a core you wrote yourself. Speech recognition is now told every core's name. The reply to a setting command uses the name you see in Settings, so it reads "Core is now Cora." rather than "Core is now cora."

The thing aboard that answers you is now called a core everywhere you read or hear it. Settings, help, panel text, spoken lines and the docs no longer say ship's AI or Ship AI, and the Persona page and capability are now Core. The name rows read Name it answers to and Keep the name when you switch cores. Saying "which persona" still works beside "which core".

The Settings page is now eight sections: Your core, Other voices, Hearing and sound, Providers, You and your records, Acting on the game, Screens, and Privacy and this install. Your core holds its character, humor, voice, callouts and the plans and reminders it raises. Other voices holds the Narrator, stories, crew seat voices, chatter and messages. Providers holds the language model, speech recognition, speech synthesis with Custom voices and what it costs, outside sources and retries; the output device moved to Sounds and levels. You and your records holds About you, Memory, notes and debrief, and Logs and goals. Speaking up is gone, Custom voices is inside Speech synthesis, the carrier's humor is now on Asset Mgmt › Carrier, and the NPC humor level is under How NPCs sound in Chatter and messages. Carrier payroll is under Chatter and messages. A story that cannot start because a voice is not ready now points to Settings, Providers, Speech synthesis.

A delivery note such as [sighs] can now sit partway through a sentence where the delivery changes, and holds until the next note or the end of the sentence. Notes follow who is speaking, so the ship's core, the crew, the carrier and story characters no longer all reach for the same note, and the same note is not repeated line after line. A reworded line, a story line or an NPC exchange is now told the notes its speaker used lately, so the same speaker stops writing the same note for the same prompt. Humor levels 1 to 6 set how much humor a line gets, and the speaker's character sets what kind; none of them names a style of wit.

Asset Mgmt › Carrier has a Captain and tower section, under Tritium, where you choose the captain's picture and the tower's picture with Change picture and Use the default. A picture belongs to the Commander flying and to the gender of the voice set: switching the captain to a voice of the other gender shows the picture you chose for that gender, or the shipped one if you chose none. Another Commander on the same install sees the shipped pictures until they choose their own. A voice that lists no gender shows no picture and both buttons are disabled. A picture you dropped by hand into `data\pictures\` as `captain.man.png` or the like no longer applies; choose it on the Carrier page instead.

The carrier captain now mentions the week's payroll the first time you dock at your own carrier after the Thursday 07:00 UTC tick, once for each week and not again after d47 restarts. The payroll is made out to the name Elite has on file for the captain. If your Captain name setting is empty, the captain says it is their own name. If it matches Elite's name, or its first or last word, the captain says the payroll is made out to them as it should be. Otherwise the captain calls the Elite name their nom de plume. The line includes the carrier's weekly upkeep when d47 knows it, and the captain answers questions about the payroll in line with this. Switch it off with Carrier payroll under Other voices › Chatter and messages.

The carrier's tower and captain now know about a fight in the system the carrier is in. If you have been attacked there, or have destroyed a ship there, since you arrived, they know how many ships you destroyed, the faction of the last one, and how many minutes ago it was. Their lines may mention it, and the captain can answer about it. They do not know who attacked you, and a fight in another system is unknown to them.

The carrier captain now knows only the carrier and your ships' names and hulls. They are given the carrier report and a list of your ships, the one you are flying and every stored one, each by name and hull with the count, and they use no tools. "Captain, how's our fuel?" is answered with the carrier's tritium, where before the captain also read out your ship's tank. Asked about your ship, a system, a station or anything else outside the carrier, the captain says it is not theirs to know and leaves it to the ship's AI. Until D47 has seen your stored ships, the captain knows only the ship you are flying.

Starting a sentence with your ship AI's name or with "D47" now speaks to the ship AI whoever else is on the line. The captain, crew, a story's cast or the Narrator stop answering, and the name is taken off before the rest is handled, so "Cora, take us in" runs the command instead of being answered "Did you mean 'take us in'?" and then offered for learning. "D47", "D-47" and "d47" all count. A name said after it still reaches that speaker: "Cora, captain, how's our fuel?" is answered by the captain. Your ship AI's name alone ends the line, and the next sentence goes to the ship AI. Speech recognition is now told both names ahead of every other name.

When D47 offers to remember your wording after a near-miss command, only a yes or a no answers the offer. A yes learns the phrase and a no says "Dropped." Anything else drops the offer without a word and is handled as an ordinary request, where before it was answered "Dropped." and ignored. A dropped offer is not counted as declined, so the same wording can be offered again.

While the captain, a hired pilot or a crew seat has the line open, a command D47 runs without the model now goes to the ship AI and the line stays open. "Request docking" runs the docking command instead of being answered that the captain has no tool for it, and the next question still reaches the captain. Naming the speaker first, as in "Captain, request docking", still goes to them, and so does a question such as "how's our fuel?".

In **Listen when I say its name**, "D47" is now always a wake word, beside your ship AI's name or the wake words you set. Every wake word speaks to the ship AI: starting with one ends any conversation with the captain, crew or anyone else, so "Warden, request docking" requests docking while the captain is on the line, and "Warden, captain, how's our fuel?" still reaches the captain. A wake word on its own ends the conversation too. When the captain, a hired pilot, a crew seat, a voice on comms, a story's cast member or the Narrator finishes answering you, D47 listens for one reply without a wake word, for as long as "How long D47 keeps listening after you say its name" is set to: "How much tritium?" after the captain's answer goes to the captain. Before, every follow-up to them needed a wake word, and a wake word handed it to whoever was on the line. No window opens after the ship AI's own answer.

Abandoning a story or finishing it now puts your Backstory back to the one you had before your first story, or to empty if you had none. Edits you made to the Backstory while the story ran are discarded. Switching passes the earlier Backstory on to the new story, so stopping that one restores it too. A Backstory that was a stock story's words when you picked is treated as none. The Abandon confirmation, the Stories intro and the Adventures help page say so.

A test drive now uses the Anthropic key in the D47_COMPARE_KEY environment variable when it is set and not blank, ahead of the stored key, so its requests bill to the dev workspace. The startup log names the source as "the D47_COMPARE_KEY environment variable (test drive)". Without the variable a test drive behaves as before, and a release build never reads it. Other providers are unchanged.

A Claude answer that calls tools now costs less. Each request now marks the end of the conversation for Claude's cache, so the second and later requests of one answer read the conversation from cache at a tenth of the input rate instead of paying the full rate for it again. The next answer reads the earlier conversation from cache too when it uses the same model and effort and comes within five minutes. The first request of each answer pays the cache-write rate, 1.25 times the input rate, on the part of the conversation not already cached. The conversation kept for each voice is now trimmed back to 60 messages once it passes 80, rather than to 80 after every answer, so its start stays the same between trims and can be read from cache. The Spend window's "Cold prefixes" row now counts an answer only when its first request read nothing from cache; writing cache no longer counts, because every answer now writes some.

The Spend window now counts a Claude request that was cancelled after it started. Anthropic bills such a request for its input, but d47 recorded nothing for it: a reworded line that ran out of time, a turn you cancelled, and an attempt that stalled and was retried. Each is now recorded with the input and cache tokens Claude reported when it began, and with no output tokens, because Claude reports those only when it finishes. The total still falls short of the bill by that output. When you cancel a turn part-way, the Spend window also counts the rounds it had already completed. OpenAI and other endpoints report usage only at the end, so their cancelled requests are still not counted.

A reworded announcement other than a narration is now asked for without thinking on Claude Haiku 5.5, Claude Opus 5 and Claude Sonnet 5, and has 4 seconds to arrive instead of 3. Reworded lines such as the session recap, the resume line and canned messages timed out often on Claude Haiku 5.5 because the model thought first, and the authored line was spoken instead while the request was still billed. Models that cannot turn thinking off still think at low effort. NPC chatter, narration and every other model-written line keep thinking as before.

A HOTAS switch position can now step through the panel's tabs. The page list on the switches page has two new entries at the top, **Next tab** and **Previous tab**. A flip to one moves the panel one tab along the bar, wrapping from the last tab to the first and back, skipping a tab with nothing on it, and landing on the tab as it was last left. A switch held in the position does not repeat. A spring-return rocker can then be next tab one way and previous tab the other. The window and the headset each step from their own tab. It works with Let a HOTAS switch operate the ship off, and while a dialog holds the panel the flip does nothing.

A HOTAS switch position can now set the power distributor. The position's list has a new **Power distributor** section with ten splits: Balanced, 4 pips to engines, weapons or systems, and the six two-system splits such as 4 systems, 2 engines. Flipping to one presses nothing if the pips already read that split; otherwise d47 presses balance power and then the power keys that reach it. D47 now reads the pips from Status.json. Pips that leave the split on their own right after it is set pause the switch, and the row names the pips as they stand when they disagree.

A HOTAS switch position can now name which interface is open. The position's list has a new **What is open** section: Cockpit, nothing open; Galaxy map; System map; Left panel; and Right panel. A three-position switch can then be galaxy map, cockpit and system map, or left panel, cockpit and right panel. Flipping to one presses nothing if it is already open; otherwise it closes whatever is open with that interface's own key, then opens the target on the next poll after the game shows the cockpit. If station services, the scanner, the codex, the orrery or the comms or role panel is open, the flip presses nothing and the switch's row says what is open. As for other switches, focus that leaves on its own right after being set pauses the switch.

A HOTAS switch pushed and released between two of d47's readings is no longer missed. A spring-return switch, such as gear down on a push up and gear up on a push down, that you flick and let spring back to the centre now counts as a flip to the pushed position and then a flip back, where before the flick could do nothing with no message. A flick counts only when the switch comes back to the position it started in and passed through one other position, so a rotary turned through a position to another one acts only on where it stops. A paused switch ignores a flick. Push-to-talk and cancel behave as before.

A HOTAS switch position can now set two things at once. The position's action list has a new **Several at once** section with five targets: lights on with night vision off, night vision on with lights off, lights and night vision both off, combat mode with hardpoints out, and combat mode with hardpoints stowed. Flipping to one presses only the parts not already in their state, so the centre of a lights switch now turns night vision off as well as the lights. A part that goes back on its own pauses the switch, and one that never arrives is said out loud, as for a single action. The switches page also lists night vision and supercruise as assignable actions.

A story chapter's current objective now has **Change objective** beside the button that was **Not for me**, which is now **Never ask this**. Change objective, on the chapter page and the Stories tab, on the desktop window and in the headset, opens a page asking "What would you rather do?" that takes your words by voice or keyboard. The ship's AI writes the current objective and those after it again from what you said and shows the reply with three buttons: **Use this**, **Keep the current one** and **Say something else**, which asks again and keeps what was said. Say "change this objective" or "change the objective" for the same page. Unlike Never ask this, it does not stop the story asking for that activity later. "Never ask this" is now also a phrase for the old refusal, and "not for me" still works. The model cannot change an objective for you.

Three more story objectives now have a button next to **Not for me**, on the chapter page and the Stories tab strip, on the desktop window and in the headset. An engineer objective reads **Show Felicity Farseer** and opens that engineer's page. A sell-data objective reads **Show unsold data** and a suit-mod objective reads **Show suits**. A button is left out when its page is not switched on. Nothing is spoken.

A story objective to board a ship now has a button next to **Not for me**, on the chapter page and on the Stories tab strip, on the desktop window and in the headset. It reads **Plan a Cobra MkIII** when you have no such ship and no plan; it opens the ship picker with that hull filled in, and committing it plans the hull and opens the new build. Once you own or have planned the ship it reads **Show my Cobra MkIII** or **Show the Cobra MkIII plan** and opens that page, or **Show my ships** when you have more than one. Nothing is spoken.

On a stock story's chapter page, **Not for me** now sits directly under the objective it refuses, inside a NEXT section, and Story on is in a separate row below. On the Stories tab, the current-story strip shows the objective with **Not for me** under it, above Read the chapter, Story on and Abandon. When the story is not waiting on an objective you can refuse, no objective is shown there. Before, the button sat beside Story on and nothing said it refused the objective rather than the story.

A story objective that waits on an engineer is now checked against the engineer list when the story is written. A name that is not an engineer is sent back to the writer with the list, and a surname or a near spelling is saved as the engineer's full name, so "Farseer" becomes "Felicity Farseer". Before, a misspelt name passed and the chapter waited on it until it was refused.

On a story's page, each character in the Cast section now shows who they are to you under their name, for example "The search-and-rescue coordinator at Ebbrells Watch, who sends the Commander the names nobody else will look for". The line is written by the story's author and says nothing the story keeps hidden. A story published before roles existed shows its characters as before, and older installs read it without trouble.

Every stock story now has a role for each of its characters, so the Cast section shows who they are and the narrator introduces them in every story, not only new ones. A story already downloaded keeps the copy on disk, which has no roles.

In a story, the narrator now says who a character is just before that character first speaks, for example "Ren is the dock hand who signs the Commander's berth." The introduction is said and posted to Messages before the character's first line in a chapter, an opening line, a clue or a finale line. In a chapter it is stored with the chapter, so the story's page shows it. A character is introduced once a story. Until the introduction has been said, the character does not react to what you did and cannot be answered. A story already running introduces each character at their next line. A character in a story published without roles is not introduced, and reacts and answers as before.

A HOTAS switch whose controller has moved to another USB port can now be found again without walking it. When Windows gives the controller a new id, the switch's card says d47 can no longer find its device. Open the HOTAS switches page and flip that switch: d47 finds the controller it moved on, points every switch learned on it at the new id, saves the switch file straight away and says, for example, "Found the device — 4 switches re-pointed." Unsaved changes on the page stay unsaved. A throttle in 4x32 mode needs one flip per block that had switches on it. If the throttle's 4x32 mode was turned on or off, the card says so and the switch still has to be assigned again. Before, every switch on a moved controller had to be walked and set up again.

When that happens, push-to-talk and cancel buttons set on the old controller id move with the switches, keeping their button numbers, and the card says so, for example "4 switches and push-to-talk re-pointed." Before, push-to-talk on the stick stopped working after the move with nothing on screen. A button on a controller no switch was found on is left alone.

A HOTAS switch position can now set your Virtual Desktop headset view to VR or Desktop. When you flip the switch, d47 reads which view Virtual Desktop is showing and presses Shift+Win+D only if the headset is showing the other one. It works at the main menu, in SteamVR Home and while another window is in front. It needs the new setting **Let a HOTAS switch change the headset view**, which is off until you turn it on and does not need **Let D47 press keys in Elite**. While Virtual Desktop is not streaming, nothing is pressed. If the view does not change after the press, the switch pauses and says to check Virtual Desktop's shortcut. While the setting is on, a switch left in a view the headset is not showing is shown with the other switches that disagree, for example "view switch: the headset is showing the desktop". For now a view is set by editing `switches.json`, as `"view": "vr"` or `"view": "desktop"`.

On the HOTAS switches page, the action dropdown now ends with a HEADSET group holding "Headset view (Virtual Desktop)". Choose it and the position's ON / OFF control becomes VR / DESKTOP, which sets the view that position asks for; Save keeps it as `"view"` in `switches.json`. Choosing an Elite action or a page for that position takes the view off again and ON / OFF returns. The entry is listed whether or not **Let a HOTAS switch change the headset view** is on. The column head IN ELITE now reads ACTION.

A switch card on the HOTAS switches page no longer shows a line of controller numbers such as "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes" under the switch name, and no message on the page quotes them.

The D47 page picker on the HOTAS switches page now groups pages under their tab: Nothing first, then TRANSCRIPT, SEARCH, STORIES, COMMANDER, ASSET MGMT, NAVIGATION and SETTINGS, each with its count. Rows show the page's word only, a page inside another sits indented under it with a `›`, and a page that is not always there says so in grey ("Only while a story is running"). A position that opens a page inside another reads, for example, `Stories › Catalogue`. Before, the picker was one flat list with the tab in brackets after each page.

A stock story chapter that is waiting for you to get a ship that can reach its Guardian beacon can now end on the ship rather than on one named hull. The new objective reads, for example, "Next: fit out a ship that can reach the beacon in Col 359 Sector XX-X c4-12." It is met by the first Loadout whose ship has the jump range for the trip (at most twenty jumps) and a fuel scoop, or by buying a fleet carrier. Before, such a chapter could only ask you to board one named ship, so buying a different suitable ship did not finish it, and boarding the named one finished it even when it could not make the trip.

d47 now starts on the Commander who last logged in, even while Elite is at its main menu, so the title bar names them and their Commander pages are filled. Before, until someone logged in the button read `CMDR` and the Commander pages were empty. A Commander you pick on the title bar or the Commanders page stays shown until you pick another or a different Commander logs in to the game, when d47 switches to that Commander. Before, a pick stayed shown whoever logged in. While nobody is logged in, the Route and Position pages say "No Commander is logged in yet" in place of naming another Commander.

When the Commander d47 shows is not the one Elite is running, including before anyone has logged in, the Commander button in the title bar turns grey and reads, for example, `CMDR ARLO KESTREL · NOT LOGGED IN`. It returns to the accent colour and the plain name once that Commander logs in, or once a different Commander logs in and d47 switches to them. The menu is unchanged. Before, the button looked the same whether or not that Commander was in the game, and only the Route and Position pages said so.

The opening greeting is now spoken as the greeting alone. Before, the model sometimes said why it had kept or changed the line ahead of "Good evening, Commander." The brief no longer asks it to weigh what is aboard or how the last session ended, and a reworded greeting that does not open with "Good morning", "Good afternoon" or "Good evening" is replaced by the authored line.

## 1.34.1 — Story progress survives a restart

A story's progress now survives a restart of the game when you are west of UTC. On startup d47 rereads the journals since you accepted the story, but it compared the acceptance time in UTC against journal file names that Elite writes in local time. The session in which you accepted the story could be skipped, along with any objective you met in it. An arrival you had already made had to be made again, and a mission handed in before that second arrival did not count.

While the Narrator, the carrier captain, the tower or a hired pilot speaks, the avatar now shows their picture in place of your ship AI's core, as it already did for a story's cast member. This works in the main window, on the mini panel and on both VR panels, and includes their answer when you speak to them, the Narrator's recap of a story, and a cast member's voice sample played from the Stories page. It is the picture the Transcript shows beside their line, or your own from **Change picture**. When the line ends, the core shows again. A speaker with no picture on disk leaves the avatar as it is. Before, only a cast member's picture replaced the core.

The HOTAS switches page now shows each position of a switch as one full-width row under the heads Position, In Elite, Means and Or opens a D47 page. The action is a dropdown that opens under the row and lists Landing gear, Lights, Night vision and the other actions by name, ON and OFF are two side-by-side buttons that grey out while the action is Nothing, and the page is a button that opens a list of pages. The position the switch is in shows a cyan bar and the word HELD, so you can check each row against the stick. Before, the action and page names were cut off ("lan…"), OFF wrapped onto a second line, a collision warning ran off the right edge, and the last switch's note could sit behind the Assign, Save and Close buttons.

A HOTAS switch position or a spoken phrase can now open ten pages one level below a tab's page: Story log, Short Story, Catalogue and Story bench under Stories; Suggestions, Activities and All lists under Checklist; Compare under Ships; Route under Engineers; and Progress under Plan. Saying a page's name, alone or after "open" or "show", such as "progress" or "open the catalogue" (or "catalog"), moves the panel there rather than going to the model. The switch page lists each one with the page above it, such as "Catalogue (Stories)". Story log opens only while a story is under way, Short Story only while a language model is configured and galaxy search is on, and Suggestions only while suggestions are waiting; a switch set to one of them does nothing at other times. Before, a switch or a phrase reached only the pages a tab's mode control lists.

## 1.27.0 — Stock stories begin

The Interdictions count on the Session page, in the session report and in your ship AI's session remarks now counts every time you were pulled out of supercruise, whether you submitted or fought. Before, it always showed zero.

While a stock story's cast member speaks, the avatar now shows their picture in place of your ship AI's core, in the main window, on the mini panel and on both VR panels. It is the picture the Story log shows, or your own from **Change picture**, drawn whole and not cropped. When the line ends, the core shows again. This includes a cast member's answer to you and a cast message you play again from the Story log. A line from the ship AI or the Narrator, or from a cast member with no picture on disk, leaves the avatar as it is. Before, the core stayed in the avatar while the cast spoke.

The Narrator now has a picture. It shows on the Narrator's turns in the Transcript and on the read page of the Narrator's messages in the Story log, where **Change picture** works for it as it does for the captain and the tower. Narrator messages already in your log show it too. The avatar does not change while the Narrator speaks.

You can now answer a stock story's cast member out loud. For ninety seconds after a cast member speaks, whether in an opening, an objective, a clue, a finale line or a reaction, whatever you say goes to them, and they answer in the first person, in their own voice. Each reply restarts the ninety seconds, and one line takes at most four replies. A command d47 runs without the model, such as "gear down", and anything that starts with your ship AI's name still go to the ship. Each answer is posted to the Story log from the member, with their picture and its clip, below what you said. Nothing opens while the story is paused or switched off, or while the game runs without Odyssey. Before, the cast spoke to you and you could not answer them.

When a hired pilot or crew seat is still in conversation and then the Narrator or a cast member speaks, what you say next now goes to whoever spoke last. Before, it went to the crew.

While a stock story runs, one of its main characters now says one line about something you did, the next time you are docked and in your ship: a death and its rebuy, an interdiction or an escape, a promotion, an engineer invited or unlocked, a ship bought, a hull you brought home below a quarter, your largest sale, bounty or exploration data sale, or a first discovery. The model picks the character from the story's main cast and writes the line as them. It is spoken in that character's voice and posted to the Story log with their picture. There is at most one a play session, and none while the story is paused or switched off. Without a language model nothing is said, and the next session tries again. To stop reactions for every story, turn off **Cast reactions** on the Stories tab.

The next chapter of a stock story or short story now mentions what you did while the last chapter ran. The writer is given up to twelve things from your journal, newest first: a death and its rebuy, an interdiction and whether you submitted, an escape, a promotion, an engineer invited or unlocked, a ship bought, a hull you brought home below a quarter, your largest sale, bounty and exploration data sale, and how many bodies you were first to discover. It is told to refer to at least one in a line from a cast member, the narrator or the ship. It is given no other Commander's name and no pilot name. With nothing notable, the writer is told nothing.

The Adventures page of the docs no longer tells you to say "next" to move a story on, because there is no such command. It now says a story waits for the game, the Narrator nudges after three play sessions and seven days, and a stock story chapter takes "not for me" for a different objective.

Say "where am I up to", "recap the story" or "what's happened in the story so far", or press Story so far, and the Narrator now says where your story stands in two to four sentences: what has happened so far and where it waits now. The button is on your stock story's page on the Stories tab, under "This is your story.", and on the page of a short story under way, in the window and on the VR big panel. The phrases recap your stock story, or with none the short story whose objective fired last. With no story under way, d47 says "No story is under way." The recap names no objective still to come and nothing of the hidden story beyond the clues you have had, and it is said only when you ask. Before, "where am I up to" answered with your location.

When a story chapter's draft cannot be used, d47 now writes it again, up to three drafts, and tells the next draft what was wrong with the last. Before each new draft, D47 says the last one could not be used and why the next can succeed, and the Stories page shows the same line. Only after the third draft fails does it stop with "The chapter could not be written in 3 drafts", which D47 now says for a later chapter as well as for chapter one. Write it again then appears on the story's own page as well as on the list. A story's page no longer shows Pick once you have picked it, including while chapter one is being written. Before, one failed draft stopped the write and Pick stayed on the page.

A story chapter's opening can now have up to three lines, each with its own speaker, the same as an objective. When the opening has you hear a cast member, such as a recording that plays every time the drive starts, that person says the words in their own voice and the narrator describes the scene. Before, the opening had one line, so the narrator had to report what the recording said. A cast member may now say their own name when introducing themselves ("This is Ines Harrow"). Any other line in which a cast member names themselves is still given to the narrator. Chapters already written keep their openings.

A story chapter no longer says a recording, call or message was played without playing it. When a line says you hear one, the person's own words are now spoken beside it, in their voice. Before, the narrator could say "The first recording has been played twice" and the recording was never heard.

A story chapter no longer sends you to a permit-locked system, such as HIP 97950, when you are already in it. You may leave and not hold the permit to return, so a chapter written while you are there now picks a place without one.

With Elite closed, a story started for a Commander other than the one you last played now speaks its opening. Before, d47 treated that Commander as off duty because the last journal named someone else, so the opening was dropped and the story card showed "Composing" until d47 was restarted. While Elite is running as another Commander, a story chapter's opening is still said as soon as the chapter is written. Other callouts and story lines are not said, and a line dropped then no longer leaves "Composing" on the card.

A story chapter or short story that finishes writing while you are on another tab now opens on the Stories tab, ready when you go back to it. Before, it opened on the tab you were on: on Settings, the breadcrumb read "Settings › " and the chapter's name, and the settings page appeared twice side by side.

A story chapter's page now has the **Story on** checkbox, so you can pause the story from there, and no longer has Abandon. Before, Abandon on the chapter's page did not end the story: it paused it and lost your place in the chapter, which then started again from its opening. To end a story, use Abandon on the Stories page.

A story chapter no longer fails when it asks you to board one of your own ships by the name you gave it. The objective now waits for that ship's hull type, such as an Anaconda. Before, the chapter was refused with "The chapter could not be written", and writing it again failed the same way.

The Stories tab now shows the **Story log** button only while a story or short story is under way, or while a story's ending is waiting for your answer. Before, it was always there, and with no story running it listed lines from stories already over.

A system search with no filters now runs and returns the nearest systems, instead of being refused. Press a column header on it to rank the whole galaxy, such as the most populous systems.

System, Station and Body search results sort from their column headers, and the sort is done by Spansh over the whole search rather than over the rows shown. Pressing Population returns the most populous systems matching your filters, pressing it again returns the least populous, and a third press goes back to nearest first. System sorts by every column; Station by Station, System, LY, From star and Updated; Body by Body, System, LY, From star, Gravity and the Share of the asked-for material. The answer head says the order when it is not nearest first, such as "MOST POPULOUS". A search opened again from History, a saved search or REFRESH keeps its sort, and a new search from the form starts nearest first. Route market offers sort the same way from the Distance and Price (or Pays) headers, over every station the market feed sent; farthest first does not go on your clipboard. The Nearest / Richest stepper on Body search is gone: press the Share header instead. The model can use the same sorts through `order_by` and `order` on `search_systems`, `search_stations` and `find_body`.

Best cargo, Construction needs, Activities and Phrases sort from their column headers. Press a header to sort by that column, press it again to reverse the order, and press it a third time to go back to the default order. Best cargo sorts every pick, not only the picks with the highest totals, so Profit / t lists the highest profit per tonne first; the # column counts from 1 in the order shown, and the spoken answer still picks by total. Construction needs sorts all columns but Order, and To buy lists the largest purchase first. Activities sorts by Activity or Last done, and the combined header on a narrow panel sorts by Activity. Your phrases and the built-in phrases each get a header row with Phrase and Meaning or Does. The headers are 44 pixels tall, so they can be pressed on the headset panel. The sort on Best cargo, Construction needs and Activities resets when you open the page again.

Stored modules, Materials, Crew and Commanders sort from their column headers. Press a header to sort by that column, press it again to reverse the order, and press it a third time to go back to the default order. On Stored modules and on each plan in Needed by plans, the sort orders the rows within each group; the groups stay where they are. Class and Combat rank sort by rank, not alphabetically, and Held / need sorts by the number held. Commanders opens sorted by Last detected, newest first. The Stored modules filter keeps the sort you chose. The headers are 44 pixels tall, so they can be pressed on the headset panel. The sort on Best cargo, Construction needs and Activities resets when you open the page again.

The star system page sorts its three tables from their column headers: Stations, Minor factions and Nearby Powerplay systems. Press a header to sort by that column, press it again to reverse the order, and press it a third time to go back to the default order: Stations by Arrival, nearest first; factions by Influence, highest first; Powerplay systems by Distance, nearest first. Pad sorts by size, largest first. The sorted column shows ▲ or ▼ after its name, and a row with no value for it stays at the bottom either way. The kind and service filters and the Powerplay state filter keep the sort you chose. The "nearest first" and "Most influence first" captions are gone, since the arrow shows the order. The headers are 44 pixels tall, so they can be pressed on the headset panel. The sort resets when you open another system or open the page again.

Missions, the Engineers directory and Custom voices have a Sort control above the list. Missions sorts by Board (the default), Expiry (soonest first) or Reward (highest first) within each band. The Engineers directory sorts by Needed (the default order), Name or Distance (nearest first, engineers with no distance last) within each group. Custom voices sorts by Added (the default) or Name. Voice pickers have a Sort control beside the filter, Listed or Name, and a Chatterbox picker sorts within each of its sections. Each control is 44 pixels tall for the headset panel, and the sort resets when you open the page again.

The Compare ships page sorts from its column headers. Press a header to sort by that column, press it again to reverse the order, and press it a third time to go back to the default order, furthest jump first. The sorted column shows ▲ or ▼ after its name. Ships with no figure for that column stay at the bottom either way. The Order by control is gone, and Minimum cargo still filters. The headers are 44 pixels tall, so they can be pressed on the headset panel. The sort resets each time you open the page.

The faction fields on the Systems and Stations search pages now suggest names as you type. After you pause, with three letters typed, a list of matching faction names appears under Controlling faction and A faction present on Systems, and under Controlling faction on Stations. Up and Down move through it, Enter or a click picks a name, and Escape closes it. The headset keyboard offers the same names as tiles. Nothing is sent while Galaxy search is off, and the search still matches the exact name.

Asset Mgmt shows the Carrier sub-tab only when d47 knows you own a carrier. It appears, between Suits and Construction, once the journal names your carrier and goes if you switch to a Commander without one. A squadron carrier alone does not show it. The same holds on the main window, the mini panel and the headset panel.

The Near box on the Systems, Stations and Bodies search pages now shows your current system as a value, such as YOUR SYSTEM · SOL, in cyan, instead of grey hint text. A line under the box says it follows you as you jump and that typing a system name searches from there instead. An empty box still searches from your current system.

The Systems, Stations and Bodies search pages now label their ranges. Each range box shows FROM or UP TO inside it, the unit is in the field label (Distance in light years, Gravity in G, Temperature in K), and a grey line under the pair says what the range will do, such as "Systems within 50 LY of SOL, nearest first." Population on Systems and Stations is now Minimum population: UNPOPULATED, POPULATED and HIGH tiles and a box, with a line saying what it asks for. How many is now Show up to, a dropdown of 1, 5, 10, 25, 50 and 100 that works on the headset panel.

Galaxy searches now return up to 100 results, and 25 when no count is given, up from 20 and 5. This covers the search pages and voice searches. Asking for more than 100 returns 100 and says "limit stops at 100".

A form's boxes now line up when one label has a mark and its neighbour does not. On the Market page the Commodity and Tonnes boxes start at the same height, as do "Credits to trade with" and "Hops" on the Trade route card.

Each Commander now has their own conversation with d47. When you pick a Commander from the title bar, the Commanders page or by saying "switch to" a Commander, d47 answers from that Commander's conversation, which is empty if they have not spoken since d47 started, and picking back resumes the first one. The model is no longer sent the previous Commander's messages under the picked Commander's settings and About me. The transcript on screen shows "Showing Commander <name>" where the switch happened. An answer still being written when you switch goes into the conversation it was asked in.

Saved searches and the search history are now shared by every Commander on the machine. A search saved or run while one Commander is shown is listed for every Commander, and your saved searches run by name whoever is shown. A saved search keeps its arguments, so one with no "near" runs from the shown Commander's current system. Files that hold a list per Commander are merged on the next start: a saved search with the same name keeps its newest save, and the history keeps the 25 most recent searches.

Each Commander now has their own pinned engineers. Picking a Commander shows that Commander's pins on the Engineers tab and under the checklist's pinned filter. The checklist filter and the partial-grades switch stay shared. Pins saved before this go to the first Commander shown.

Each Commander now has their own last route plan per planner. Picking a Commander shows that Commander's plans on the route pages and in route answers, and an arrival moves forward only the plans of the Commander who flew it. Plans saved before this go to the first Commander shown.

A Commander's log now covers only the Commander shown. It holds the events between that Commander's Commander or LoadGame entry and the next one naming somebody else, so a day two Commanders flew no longer mixes them. "The last session" is that Commander's last session, and the day list in the Logbook offers only days they flew. New logs go to `commander-log\<Frontier ID>\`; logs already in `commander-log\` stay where they are and still show.

While you view a Commander other than the one in the game, the Progress and plan pages, the mini route, Best cargo and On this body no longer draw the game's live route or position against them. They show one line instead: "CMDR Kestrel Vane is in the game; their route shows when they are the Commander shown." Best cargo offers no Route end tile, On this body shows no distance, and the Power page shows no measured draw. Picking the Commander in the game brings it all back.

Picking another Commander now redraws the pages you have open, on the window, the headset panel and the overlay, so Bookmarks, Activities, Phrases, Checklist, Suits, Galaxy search, Missions, Construction and Engineers show the new Commander. A mission, construction site or search answer chosen for the previous Commander is cleared.

The Commander name in the title bar no longer stays an orange block with unreadable text after its menu closes, for example after a theme change.

A Commander you pick in place of the one in the game now has their own credits, taken from their newest LoadGame in the journals. A story chapter that boards a ship checks the ship against their fleet by hull, so a ship they own, such as an Alliance Chieftain, is no longer treated as one they must buy. A Caspian Explorer in the fleet now allows a long haul.

On the Stories tab, opening the catalogue no longer keeps the Stories page as a column beside it; the breadcrumb is the way back. The catalogue is now named Catalogue on its button, its breadcrumb and its heading, and each row shows at most two lines of the story's blurb. The story page still shows the whole blurb.

When a story chapter or short story cannot be written, the Stories and Short Story pages now show and say one plain sentence: "The chapter could not be written. Write it again and I will try a new draft." They no longer read out the checker's reasons, such as the cost of a ship the chapter wanted you to board, which were written for the model. The reasons are in the log.

A story chapter no longer fails with "The model did not write the objectives" when the model spends its whole output allowance thinking. Story chapters and ship plans now allow 16,000 tokens of output, so thinking cannot use up the room for the answer. You pay only for the tokens the model uses.

The tech broker's Corrosion Resistant Cargo Rack is now listed as 4E, the one size the broker unlocks, instead of "2D and 4D", which are not sizes the module comes in. The recipe is unchanged.

The first time a new version of D47 starts, it zips your data folder into `data\backups` before it reads anything, and keeps the newest 25 zips. The zip holds your settings, keys, stories, checklists and the other files D47 writes for you. It leaves out the large folders that D47 downloads or records: speech models, music, ship art, avatar clips, story downloads, recordings and logs. Starting the same version again takes no new zip.

The ship collection goal no longer needs the Cobra MkIV. Only Commanders who bought Horizons before February 2016 can own one, so the goal counts it only when you own it: without one the goal is 47 hulls, with one it is 48. D47 never suggests buying it, and a hull missing from the ship table no longer counts toward the total.

The headset avatar now animates. On the big panel and on the mini panel the core's clip plays at its true speed over the panel's avatar, instead of a still frame. It steps aside for a modal, a chooser or the resize handles, and the still frame shows while it does.

The Stories tab's **Ask for one** button is now **Short Story** and sits after **Stories**: the bar reads Stories, Short Story, Story log. What a Commander reads or hears no longer says "adventure": a story D47 writes for you is a short story, and anything that covers a stock-story chapter too is a story. The voice commands are "ask for a short story", "change the short story", "accept the short story" and "reject the short story"; the old "adventure" phrases still work. The capability is now called Stories, the callout toggle Story objectives, and the setting Notable places for short stories.

The Stories tab's **Messages** button and page are now **Story log**, and the bar reads Stories, Short Story, Story log. The button shows "Story log (2 new)" while lines are unread. The page groups what was said during stories: lines waiting for your answer first, then one group per story headed with its name, the story with the newest line first, then Earlier stories for a story that no longer exists and Not tied to a story. Narrator lines now show the Narrator's name and a core's lines show the core's name as the sender. The page was renamed because "messages" also means Elite's in-game inbox.

Each Commander now keeps their own answer to "Your Commander is" on a story page. Before, one answer was shared by every Commander on the installation, so a second Commander could be shown the other version of a two-version speaker. A Commander who has not answered yet uses the installation's existing answer.

Picking another Commander now redraws the Stories tab for them, in the window and on the headset: the Stories list, the story pages, the Stories tab's own page and the mini panel's story. A chapter page or next-chapter form that belonged to the last Commander is closed. A button, a confirm prompt or a star vote left from before the switch does nothing and the status line says "The Commander changed. Nothing was done." This fixes Switch on a story page abandoning the newly picked Commander's story, and Story on, Not for me and ratings acting on a different Commander from Resume, Abandon and Write it again.

Hero Ferrari's unlock now asks for 5 Settlement Defence Plans, the figure since Frontier's update 4.0.18.08, not 15. A Commander holding 5 plans is no longer told the hand-over is not held.

On the headset, the Systems, Stations and Bodies search pages now draw their form. Each page has two full-width views: the form, with the saved searches, the filters, RUN SEARCH and CLEAR; and the answer, with EDIT SEARCH, REFRESH, SAVE THIS SEARCH and the table. RUN SEARCH, a saved search and REFRESH show the answer; EDIT SEARCH returns to the form with the values that were run. A search asked by voice fills the form and shows the answer. SAVE THIS SEARCH and the forget tile work on the headset. The desktop window is unchanged.

Settings › Voice and hearing now opens a Custom voices page where you can record a voice or import one from a WAV, MP3, M4A, WMA or FLAC file, give it a name, gender, pitch and pace, hear the 7 seconds D47 will keep, and save it. Saved voices can be played, renamed and deleted, and appear under the voice picker's Custom filter, so a core can speak in one; D47 never chooses them for strangers. This replaces the earlier rule that no clip could be imported and that only a recording of your own voice was allowed. Your voice has moved to the top of this page, and the sentence it used to ask you to read is gone: any words will do. Custom voices stay on this PC, readable only by your Windows user, and are never sent anywhere or included in a donation.

On the headset, the drawn keyboard for a system-name box shows up to eight matching system names as tiles above the keys once you have typed three characters, and refreshes after each key, delete, clear or spoken word. Pressing a tile fills the box, closes the keyboard and does what picking the name does in the window; on the System page it opens that system. Nothing is shown or sent while galaxy search is off.

The headset panel now has four things the window already had: Settings › Phrases, the draft actions on a story, Fleet › Crew seats, and the talk section on a ship build.

On the headset, pressing a setting's label shows its help in a line under the label, and pressing it again hides it. Pressing a field name on the Raw Journal page shows what it means in a box under the row. One such line is open at a time. The transcript also has a Scroll what is shown above the fold tile, which the window keeps in the right-click menu.

The headset panel now has the SPEND row under the transcript, and pressing it opens the spend figures on the headset.

The headset panel now has the search field with its previous and next buttons, Copy page, and the filter field on every page that has one. A ray press on a field opens the drawn keyboard, and Done runs the search or filter on what you typed. The buttons are 44 pixels tall.

When a voice slot speaks through Chatterbox, each row in the voice picker now says whether its clip is on this PC: Shipped, Fetched or Custom in yellow, Not fetched in grey with the clip's size, Fetching in white, and Fetch failed in red with a Retry button that fetches again without playing. A row also shows its gender, pitch and pace, the voice in use is tagged In use, and the list is split under On this PC and Fetched when played or chosen, with the voice suggested for the role first. Pressing play on a voice that is not on this PC fetches it first, and the row moves to On this PC when the clip arrives.

A Chatterbox voice row in Settings now shows the voice's name, its gender, pitch and pace, and whether its clip is on this PC. While the chosen voice is not on this PC, a line under the row names the shipped voice D47 speaks in until it arrives: "Until Lucie arrives, D47 speaks in Marlow." A voice not yet fetched has a Fetch now button, and a failed fetch shows the reason with Retry and Choose another. The line goes when the clip arrives. Until Chatterbox itself is downloaded, the line names no voice, since D47 speaks through Edge then. In the picker, the line above Back and Use this says what choosing the highlighted voice fetches and who speaks meanwhile.

On docking at a station that is under attack, damaged, under repairs or abandoned, your core says so once: "Galileo is under repairs." A station under construction is not mentioned. Callouts has a Station damage toggle, on by default.

D47 keeps the last 25 galaxy searches you asked, for each Commander, in `search-history.json`, with the answer each one gave. That covers system, station and body searches, whether asked by voice or on the search pages, and a system you name by voice or by typing on the System page. Asking the same search again keeps the newer one.

Typing three or more letters into the System page's field lists, under it, up to eight systems whose names start with them. Up and Down move through the list, Enter or a click opens the highlighted system, and Escape closes the list. Enter with nothing highlighted looks the name up as before. The letters go to spansh.co.uk only while "Look things up in the galaxy" is on, and Settings › Privacy and egress says so.

The same list appears under the Near field on Search › Systems, Stations and Bodies, under Destination, From and the carrier route's To and From on Navigation › Plan, and under Sell in on Navigation › Best cargo. Picking a name only fills the field; it does not run the search or plot the route. A field filled by a spoken or saved search, or from Fleet › Carrier, does not open the list.

The Home system box in Settings › Callouts lists the same system names as you type. Picking one fills the box and saves it as your home system. With "Look things up in the galaxy" off the box lists nothing, sends nothing, and can still be typed into and saved.

HISTORY on the System, Systems, Stations and Bodies pages opens your last 25 searches in a full-page list, newest first, with a Kind filter. Open shows a galaxy search's stored answer and how long ago you asked it, with no new request, or a system's page fetched again. REFRESH asks the same question again from the system the search measured from.

A line you type into the Checklist page's Add box is still added at once. D47 then sends that line, once and on its own, to the AI to ask whether docking, arriving, buying, selling or a carrier buy order would tick it. If the AI names one, a proposal such as "Tick \"Buy 5 Hydrogen Fuel\" when you buy 5 Hydrogen Fuel" waits under Suggestions for you to accept or decline. If the AI names none, no AI is set up, or the request fails, the line stays one you tick yourself. A line you edit, tick or delete before the answer arrives is left alone. Settings › Privacy and egress lists the request.

The AI can propose a watch for a checklist line, either on a new line or on one you already wrote. The proposal names the condition, for example "Add \"Buy 5 Hydrogen Fuel\" to the custom list, ticked when you buy 5 Hydrogen Fuel at Archambeau's Serenity +", and nothing is armed until you accept it. Counting starts when you accept, so an event between the proposal and your yes does not count. A condition that cannot work is refused with the reason, and nothing is recorded.

A line you wrote on the checklist can carry a watch in `checklist.json`: docking at a station, arriving in a system, buying or selling a commodity, or your carrier's buy order for one. When the journal records a matching event after the watch was set, the line ticks itself and D47 says so under Checklist changes ("Ticked "Buy 5 Hydrogen Fuel": you bought 5 Hydrogen Fuel."). The line keeps its checkbox, Remove fulfilled items never removes it, and unticking it by hand starts the count again. The Checklist page and `get_checklist` show what the line is waiting for, with the count so far for a buy or a sale. The AI can propose a watch, and you can write one in `checklist.json`.

A search you ask for opens its page on the window and in the headset, with the answer drawn: "find anarchy systems in boom" opens Search › Systems, a station search opens Stations, and a body search opens Bodies. Quick searches and your saved searches, run by their names, do the same. A search that is refused moves nothing, and a window or headset that has a chooser open stays where it is.

Search has a Bodies page after Stations. The form takes every filter the body search takes: where to search out from and how far, whether to show the nearest or the richest, the body's type, surface material, landable, terraforming candidate, tidally locked, volcanism, atmosphere, gravity, temperature and distance from the star, a surface signal or ring hotspot with its exact count, ring type and reserves, and the system's power and Powerplay state. With RICHEST and a material, the answer says the rows are the richest in that material among the nearest 50 that carry it, and a column shows each body's share of it. A search asked by voice shows here too. The SAVED row says there are no quick searches for bodies and lists your own.

A Chatterbox voice picker offers four filters above the name box: Gender (Any, Female, Male), Pitch (Any, Low, Mid, High), Pace (Any, Slow, Even, Brisk) and Show (All, On this PC, Custom). They combine with each other and with the name box, and a count beside the name box reads, for example, 53 OF 847 VOICES. A custom voice is matched by its own pitch and pace, and one saved with no gender appears under Gender Any only. This applies to the Voice, Carrier captain voice, Carrier tower voice, Narrator voice and each seat voice rows when their slot speaks through Chatterbox. Kokoro, Edge and the other providers keep the single Gender filter.

Search has a Stations page after Systems. The form takes every filter the station search takes: where to search out from and how far, the station types (none ticked means every type but fleet carriers), the landing pad, how far from the star, services, material trader, technology broker, the station's economy and state, allegiance, government and controlling faction, a module, its class and rating or a ship sold there, and the system's economy, power, Powerplay state, population and colonised systems. CHOOSE SERVICES opens a page of every service to tick; each one chosen shows as a tile on the form, and pressing its ✕ removes it. RUN SEARCH runs it. The answer lists each station with its type, system, distance, largest pad, distance from the star, which of the services you asked for it has, and when it was last updated. A search asked by voice shows here too, and REFRESH, SAVE THIS SEARCH and the SAVED row work as on Systems.

The setup wizard no longer lists what each choice sends. The Keys step has no line under a key, and the Ready step has no "What leaves this machine" section and no button to open Privacy and egress. Settings › Privacy and egress is unchanged. The provider comparison, install, first-run, speech, galaxy and logbook help pages drop the same material.

Search has a Systems page beside System. The form on the left takes every filter the systems search takes, from where to search out from and how far, through allegiance, government, economy, security, population and colonised systems, to the factions and Powerplay. RUN SEARCH runs it. The answer on the right lists each system with its controlling faction, distance, allegiance and government, economy and security, population, power and when it was last reported. A row opens that system's page. A search asked by voice shows here too. REFRESH runs the same search again. SAVE THIS SEARCH names it, so saying its name followed by "search" runs it. The SAVED row lists the quick searches and your own, and ✕ forgets one of yours. With "Look things up in the galaxy" off, RUN SEARCH is disabled and the page points to the setting. In the headset the page shows the answer and the saved searches.

A recording that is too short is refused with its length and the 5 s minimum, without the advice to read the whole sentence, since a voice can come from a file and the sentence is only a suggestion.

A story can pin a cast member to Edge Neural as well as Kokoro and Chatterbox. A story may not pin a billed provider (ElevenLabs, OpenAI or Cartesia); one that does is not loaded. While a story with an Edge pin runs, Privacy and egress names the character and the story under Edge Neural.

Ask "what kinds of missions are there" to hear the board's groups with their mission types, or "where would I find a disable mission" to hear the faction states, governments and economies that type's names were recorded under, whether it needs a Federation or Empire navy rank and whether you hold one, and whether it comes from engineer bases. The answer gives the likeliest circumstances, not what a board offers. An unknown type is answered with the nearest names.

Ask "find me somewhere to get a disable mission" to hear the five stations likeliest to have one, within 50 light years unless you say otherwise. Each comes with its system, how far it is and how many factions there are in a matching state. Every faction present in a system counts, not only the controlling one. Fleet carriers and other stations without a mission board are left out, and for an on-foot type so is any station without a social space, unless it is a settlement. A type that needs a navy rank you do not hold says so first, and an engineer's courier mission names the nearest engineer bases instead of searching. The Galaxy search entry in Privacy and egress names the faction states or governments the search sends.

The logbook's date range is chosen on a full page instead of a pop-up calendar, so it works in the headset. The FROM and TO tiles open a list of the days you have journals for, newest first, and a day can also be typed as year-month-day.

The spend dialog's reset windows open in the page instead of a pop-up menu, so they work in the headset. The Reset list is a section at the end of the dialog, and the footer keeps the help mark and Close.

A story's cast now speaks through the provider you set for the ship, unless the story marks a character `pinned`. A pinned character, and one on your own recorded voice, keeps the voice the story gave it. Any other character speaks in the crew voice until it has a voice of its own, and if a voice you chose for it fails, in the ship's provider rather than Kokoro. A story with no pinned character starts without the Kokoro download.

Each character a story does not pin now gets its own voice from the ship's provider when the story starts or resumes, and again when you change the ship's provider while it runs. The language model chooses from the character's name and description, limited to the gender of the voice the story wrote it for; with no language model, a voice of that gender is picked from the list. No two characters of a story share a voice, and none shares the ship's or the Narrator's, while the provider has enough voices. The pairing is kept for each provider, so changing back reuses it. A voice you chose for a character still wins, and Reset voices forgets the pairings. The Language model entry in Privacy and egress says what is sent to choose them.

Station search can filter by the station's own state. `station_state` takes Construction, Damaged, DamagedHuman, UnderAttack and UnderRepairs, one or more, so "find the nearest damaged station" and "which stations near me are under attack" work. Each result says whether the station is under construction, damaged, under attack or under repairs.

Every control you press in the panel is now 44 pixels tall, the size a ray hits in the headset. That covers the breadcrumb above a picker page, the session figures button, dropdown pickers and their options, the Phrases page's Say box, Add and ✕, engineer names, the level cells in Transcript › Log File, the checklist's "completed" box, the rows on a choice page, Powerplay system rows, and "Change filters" and "Plot" on Best Cargo. Dropdown pickers are 4 pixels taller when closed.

Station search offers every service Elite has, not only 19 of them. `services` now also accepts Contacts, Missions, Missions Generated, On Dock Mission, Tuning, Workshop, Refinery Contact, Powerplay, Shop, Livery, Social Space, Squadron Bank, System Colonisation, Construction Services, Fleet Carrier Administration, Fleet Carrier Fuel, Fleet Carrier Management and Fleet Carrier Vendor, so "find a station with a workshop near Sol" works. The earlier 19 keep their names.

A story character's Change voice picker now works like the Settings voice picker. It has the Gender filter where the provider's voices carry a gender, plays ElevenLabs' free sample for a voice that has one and keeps the speech button for the line, and words the cost the way the Settings Voice row does. The provider list shows each provider's key status, such as PAID · NEEDS KEY. A provider with no stored key stays on the list, and playing one of its voices says it needs an API key instead of failing.

The setup wizard skips the Listening and Talk button steps on a machine with no microphone, and its Ready step shows Listening as "No microphone found". The listening settings are saved as they were. Set listening and push-to-talk in Settings once a microphone is plugged in.

A test drive no longer opens with the unknown-settings-keys message or the "is available" update band. The unknown-keys message is also no longer spoken at startup. A settings file d47 refuses still shows its error, and the update check from the Version row still works. An installed build behaves as before.

On a first launch, d47 says nothing until the setup wizard closes. Before, a fuel warning and "I have no language model right now" were spoken over the wizard. Lines that come up meanwhile are dropped, not said afterwards, and d47 no longer announces the missing language model once the wizard has been shown, whether you pressed START, skipped the key or closed it. A launch that does not open the wizard speaks as before.

The first line after setup is spoken instead of failing with "No OpenAI voice has been chosen. Pick one in Settings." Until d47 has chosen the core's voice, which takes a few seconds after START, lines are spoken in the provider's first listed voice: Alloy on OpenAI, and the first voice in your account's list on ElevenLabs and Cartesia. That voice is not saved, and the next line after d47 chooses uses the chosen voice. The "No … voice has been chosen" message now appears only when the provider lists no voices at all.

The setup wizard closes when the Kokoro download finishes. Before, it stayed on "335 of 335 MB" because the spoken "Local voice installed" line failed after the download and the wizard never saw the failure. Setup no longer speaks that line, and a download that fails for any reason shows its message with RETRY. From the Settings row, the line is spoken in a Kokoro voice even before one is chosen, and a line that cannot be spoken reports "Downloaded, but the voice could not speak" instead of a failed download.

The push-to-talk button warning in setup and in the listening status checks only the controller the button is on. Before, a binding on button 9 of your stick produced a warning for button 9 on your throttle. When Windows shows the controller as several devices, as a WinWing throttle does, the warning says Elite binds that button number on one of them and that d47 cannot tell whether it is the one your button is on.

A route already plotted when d47 starts no longer gets a fuel warning on launch, such as "Bolones is class Y — no fuel there." ahead of the greeting. A fuel warning is given when you plot a route or jump while d47 is running.

Galaxy search can find a system where any faction present is in a state or has a government, not only the controlling faction: "find a system within 40 light years with a faction in Civil Unrest or Expansion". Each system in the answer names the faction that matched and its state or government. A faction name, a faction state and a faction government asked together match one faction. A station search can now filter by the station's own economy; the existing economy filter still means the system's.

The Stutter preset starts speaking sooner: it works as the voice arrives and holds back 300 milliseconds, instead of waiting for the whole line. What it does to the line is unchanged. The Ghost preset still waits for its whole line because Whisper does.

Reverse reverb's swell into each word now lasts at most 0.3 seconds, and it works as the voice arrives, holding back about 40 milliseconds. The sound changes: the reverb's long tail no longer reaches back before a word. The Ghost preset still waits for its whole line because Whisper does.

COVAS on Edge Neural now always speaks in Sonia, a British woman's voice, and no core or carrier role is given a child's voice (Maisie and Ana are never cast automatically, though you can still pick them). An install whose core was given one is cast again once.

A line for Kokoro or Chatterbox is spoken by Edge Neural in `en-GB-SoniaNeural` until that model is downloaded, instead of failing with "not downloaded yet". Every speaker uses that one voice meanwhile, and the next line after the download uses the voice you chose, with no restart. Until then the text goes to Microsoft (`speech.platform.bing.com`); the Privacy and egress section, `get_data_egress` and the setup Ready step say so, and the Settings voice row says that Edge speaks until the download finishes.

Setup now downloads the voice you chose. When you pick Kokoro or Chatterbox and its model is not installed, START opens a download step with a progress bar and the megabytes fetched, and the wizard closes when the download finishes. Cancel keeps your choice, and the Settings row still offers the download. The Ready step says how large the download is before you press START.

A core's line in the transcript always shows its shipped portrait. A `core.*.png` file placed in `data\pictures` is ignored.

The Helmet comms and Hologram Guardian presets now start playing before the sentence group has fully arrived. The effects sound as they did; as with the presets that already started sooner, loudness can shift slightly at the start of a line.

The Vocoder, Deep core, Flanged vocoder, Hive chorus, Shimmer core and Respirator Guardian presets now start playing before the sentence group has fully arrived. Cylon, Pitch down, Octave-down layer, Hive and Shimmer now work as the voice arrives, each holding back at most about a sixteenth of a second. The effects sound as they did; as with the presets that already started sooner, loudness can shift slightly at the start of a line.

The 8-bit computer, Flat robot and Stepped synth Guardian presets now start playing before the sentence group has fully arrived. Bitcrusher, Monotone and Stepped pitch work as the voice arrives, holding back the first 300 milliseconds and a little under a tenth of a second more. The effects sound as they did; as with the presets that already started sooner, loudness can shift slightly at the start of a line.

Chatterbox now applies the respellings in `pronunciations.json`, so `"Supercruise": "super cruise"` fixes how it says supercruise. IPA entries still work only for Kokoro.

A core switched back in reacts to the time since it left, not since it last came aboard. A core aboard when Elite closed counts its absence from then.

A line still waiting to be spoken when D47 closed could throw an error on the way out. It is now dropped quietly.

A core picked on the panel while a ship switch was landing is no longer announced as the ship's core, or kept silent.

A direction taken for one core now stays in the prompt after renaming the ship AI, switching core, or a story putting the stock core aboard.

Help has two new pages under General help. First run walks through the setup steps and says where to get each provider's key and which choices it also covers. Choosing providers compares the conversation, voice and listening providers by cost, key, where they run and what leaves the machine.

Changing two speech settings at nearly the same moment could leave the voices wired to the older settings. The voices now follow whichever change was made last.

A switch press or spoken answer in the first seconds after start-up is no longer dropped while the panels are being set up.

The first launch now opens a seven-step setup in place of the key window. It asks which AI to talk to, which voice to speak with and how D47 hears you, listing every provider with one line on it and whether it needs a key, is free, or runs on this PC. It then asks only for the keys those choices need, each key once even when it serves more than one choice, with a link to the provider's key page and a line saying what is sent where. The talk button step binds a stick button or a key, warns when Elite already uses it, and sets Hold, Toggle, Always on or Wake word. The last step lists the choices and every destination they send to. Nothing is saved until Start, and Skip setup closes it with every setting unchanged. A choice whose key was skipped is saved as the free choice for that part: no AI, Edge Neural voices, or listening on this PC. About › Set up keys opens the same setup with the current settings chosen.

A key bound to Scroll Lock is now shown as Scroll Lock rather than Scroll.

Push-to-talk now starts out bound to Scroll Lock instead of Right Shift, a key Windows gives no shortcut and few games use. Scroll Lock can also be bound by hand. An install that already has a push-to-talk key saved keeps it.

Privacy and egress has a new entry, Your custom voices: a voice you record or import for Chatterbox is kept in `data\voices\custom` on this PC, encrypted for your Windows user so no other account can read it, and is never sent anywhere or included in a donation. Nothing in the app records or imports one yet.

Chatterbox now lists your recording as Your voice and each custom voice after its own voices, and speaks in them. The Chatterbox voice pickers gain a Custom filter that shows only those. A custom voice is never drawn for a stranger, an NPC or an automatic core voice. If a custom voice is deleted, or does not open for this Windows user, its lines are spoken in a shipped voice of the same gender, pitch and pace, and the log names it by its `my-` id only. A message spoken in a custom voice keeps its clip encrypted, and deleting the voice deletes those clips and keeps the text.

A new help page, Donate your journals, walks through sending your journal history from the Help improve page, with a screenshot of each step and the buttons to press marked.

The consent text for an incident excerpt and a journal history on the Help improve page, and the Help improve, Donation privacy and Data retention help pages, named a replay tool that does not exist. They now say what a donation is used for: reproducing and diagnosing defects against play that really happened. A journal history may now also be read by the maintainer, including through AI coding sessions, to learn what the game writes for parts of the game d47 does not handle yet. Only event names, field names, counts and game symbols from it are published. A journal history is still kept until you ask for it back, and an incident excerpt is still deleted after 30 days.

Transcription is now biased towards the name of every command d47 can give, including the Galnet, headset, panel, FSS, on-foot and fighter commands in this release. Previously the names after the first 120 were left out.

Changing the voice provider, or switching the local voice's build, while the local voice was speaking could close d47 or leave its model open. A local voice now finishes the line it is speaking before it is closed, and a closed one opens nothing again.

Changing speech settings while a core change, a voice download or a voice repair was running could corrupt the list of voice clients, or close one that was about to speak. Speech wiring now changes on one thread at a time.

A stated memory that expired while d47 was closed is now shown on the panel at startup and no longer spoken. Expiries found later are still announced.

A leftover diagnostic in the panel wrote a file four seconds after the panel opened, to a folder that exists only on the developer's PC. On any other PC the write failed, which could close d47. It has been removed.

The Page settings toggle is now a tile at the right end of the page's title line, reading `▸ SETTINGS 9` (the count of rows) and `▾ SETTINGS 9` on solid cyan while open. It replaces the bar at the bottom of Transcript › Log File, Stories, Asset Mgmt › Ships and Asset Mgmt › Carrier, in the window and in the headset panel. Open, the settings rows appear at the top of the page under the sub-tab row and push the page down; collapsed, nothing is left at the bottom. The open or closed state is still remembered per page, and a settings search match under "On other tabs" still opens it.

Chatterbox now offers 847 voices, each with its gender, pitch and pace. Twelve come with D47; any other is fetched once from github.com the first time you pick it, press Play on it, or a line is due in it, and is spoken offline from then on. Incoming messages and the voices D47 chooses for cores draw from all of them. A line whose voice has not arrived within 3 seconds is spoken in one of the twelve, and the log names both. A clip in `data\voices\chatterbox` is used only while its size and SHA-256 match the list that came with D47. A file dropped there is never loaded, and one that does not match is deleted. Privacy and egress has a new Chatterbox voices entry.

The search row on the title line now reads in the order you use it: the search field with the match count and a clear ✕ inside its right end, then the previous and next match buttons, then a divider, then Copy and "Help improve D47…" at the right edge. The CLEAR button is gone; the ✕ in the field clears the search and puts the cursor back in the field. A page with only a tool and no search field keeps the tool at the right edge with no divider.

A place in the panel named in a message is now a link. "Settings" in an error such as "Pick one in Settings.", a path such as "Asset Mgmt › Ships" or "Settings > About", and "the Navigation tab" are drawn in cyan, underlined when the pointer is over them, and clicking one opens that tab and, where the path names it, that view of the tab. Any open dialog page closes first. This works in the error banner, in transcript turns including failed ones, and in the notices on pages, in the window and in the headset panel. A link clicked in the headset moves the headset only. A tab name on its own, as in "Commander, the route is plotted.", stays plain text, and so does a tab the panel does not have. Selecting and copying text across a link copies the plain text.

A "Settings" link in an error about one setting now opens the settings page on the section holding that setting, in the window and in the headset panel. "No Chatterbox voice has been chosen. Pick one in Settings." opens Voice and hearing › Its voice. A missing API key opens the section with that provider's key, and a hotkey that could not be registered opens the section with that hotkey. An error that names no setting still opens the Settings tab. A link that wraps onto a second line now responds to a click.

Say "play galnet", "pause galnet", "galnet play" or "galnet pause" to press the Galnet audio play/pause key; "next galnet story" or "skip galnet" to skip forward; "previous galnet story" to skip back; "clear galnet" or "clear the galnet queue" to empty the queue. Play and pause share one key and the game reports no Galnet playback state, so both press the toggle.

Say "recentre the headset", "recenter the headset", "reset the headset", "recentre the view" or "reset vr" to press Elite's headset reset key and recentre the view in VR, in the ship, the SRV or on foot.

Say "next tab" or "next page", and "previous tab" or "previous page", to change the tab of the open panel; "ui focus" or "focus the panels" to take focus of the panels; "quick comms" or "open quick comms" to open quick comms, including on foot; "orbit lines" to toggle the orbit lines; "increase sensor range" or "sensor range up", and "decrease sensor range" or "sensor range down" to change the sensor range in flight. "Galaxy map home" or "centre on my system" centres the galaxy map on your system, and answers that the galaxy map is not open when it is not.

Say "open the fss", "full spectrum scanner" or "fss" to open the full spectrum scanner in normal space or supercruise. Inside it, say "close the fss", "leave the fss" or "exit the fss" to leave, and "discovery scan" or "honk" to hold the discovery scan key for the charge time. Outside the FSS, "close the fss" and "discovery scan" answer "The FSS is not open."; "open the fss" while it is open answers that it is already open. None of them presses a key in those cases.

On foot, say "shields on" or "shields off", "use a medkit", "energy cell", "energylink", "profile analyser", "suit tool" (also "genetic sampler" or "arc cutter"), "primary weapon", "secondary weapon", "sidearm", "holster", "frag grenade", "emp grenade", "shield projector" or "mission help" to press the matching on-foot binding. Shields have one key and the game reports no shield state, so "on" and "off" both press the toggle.

Flight actions now work in a fighter you launched from your ship: flight assist, boost, the throttle settings, the power pips, fire groups, the lights and the targeting commands press the same keys as in the ship. Gear, scoop, hardpoints, the frame shift drive and the panels are still refused there, with "in a fighter".

The flat overlay now has every page the headset's mini panel has: Missions, Standing, Statistics, This session, Commanders, Raw Journal, Log, Routing, the rest of Assets and Engineers, and it keeps them current as the game changes. System names and your Commander's name show as chips in its transcript, as they do in the headset.

Say "next hostile", "previous hostile", "next target", "previous target", "target ahead", "highest threat", "next subsystem", "previous subsystem", "target wingman one" (or two, three), "target wingman's target" or "wingman nav lock" to press the matching targeting binding. "Target ahead" also works in the SRV. Say "fighter orders", "recall the fighter", "fighter defend", "fighter engage at will", "fighter attack my target", "fighter hold fire", "fighter hold position" or "fighter follow me" to give the fighter that order. "Chaff", "shield cell" and "ecm" fire the countermeasures in normal space; the ECM key is held for four seconds so it fires on release. Macros can use the countermeasures.

Say "full throttle", "full speed" or "throttle to a hundred" to set the throttle to 100 per cent. In normal space, "full reverse", "half reverse", "throttle to minus twenty-five", "throttle to minus fifty", "throttle to minus seventy-five" and "throttle to minus a hundred" set the reverse throttle, and "reverse thrust" toggles it. Each presses the matching binding from your Elite controls.

Actions that were refused in a mode where Elite has a binding now work there. In the SRV, "next fire group", "previous fire group" and "analysis mode" press the SRV bindings. On foot, "galaxy map", "system map", "comms panel" and "lights" press the on-foot bindings; "lights on" and "lights off" press the toggle there, because the game reports no state for the suit light. Supercruise is now a switch that works in normal space and in supercruise: "drop out", "exit supercruise" and "drop out of supercruise" leave it, and asking for a state you are already in answers that it is so and presses nothing. Sending a chat message still needs the ship or the SRV.

Say "newest", "latest", "jump to the newest" or "scroll to the bottom" to go to the newest line and follow the transcript again. It works on the window, the headset panel and the flat overlay, including the mini panels that have no ↓ Newest button.

Ship and SRV switches answer to the same set of wordings, so more of them act at once instead of going to the model: "raise the landing gear", "toggle landing gear", "turn the lights off", "switch on night vision", "deploy the cargo scoop", "scoop away" and "flight assist" all work, and so do "boost", "jump", "warp", "cruise" and "turret view" on their own. "Analysis mode" and "combat mode" still set that mode, and "switch to combat mode" works too. The short reply after a command names it by its shortest form: "Aye, hardpoints out." rather than "Aye, deploy hardpoints.", and "Aye, hyperspace." rather than "Aye, engage.". Settings › Phrases lists each control as a few patterns, such as "[gear|landing gear] [down|up]", rather than every wording. A macro can no longer take a command's name with "the" added or dropped.

Game commands now run when you add a courtesy such as "please", "now" or "for me": "gear down please", "lights off now" and "engage supercruise for me" act at once instead of asking "Did you mean…?". Phrases you taught D47 work the same way.

Game commands now match however the transcription writes them. Digits, "%", "per cent" and hyphens are read the same as the words, so "throttle to 25", "50 percent" and "75%" set the throttle without the model. "Super Cruise", "heatsink" and "hard points" match "supercruise", "heat sink" and "hardpoints". Speech recognition is also told to expect the names of the ship's controls, such as "landing gear", "cargo scoop" and "silent running", after the names from your journal, as far as each service's limit allows. A phrase you teach D47 is refused when it means the same as a built-in command written another way, such as "throttle to 50". With local Whisper, a long list of names now keeps its first names rather than its last.

The mini panel, in the headset and on the flat overlay, no longer stops following the newest turn when it is first filled. A panel that began following and then had its content re-measured could end up parked above the newest turn with no button to return it.

The headset panel and the flat overlay now show the same page as the window. Changing tab, choosing another view of a tab such as Commander › Standing, opening a page within it and going back all carry over, and the headset redraws at once rather than at the next transcript line. After a restart they open where the window opens, and the headset no longer remembers a tab of its own. A dialog or a chooser opened in the window stays in the window, and a panel that has no such tab or page stays where it is.

The wording of the directions a debrief proposes now comes from your conversation model, not the Background model. Each one is still yours to take or decline.

The Settings row "Model for the quiet calls" is now "Background model", and the model status report and the notice that a default changed use the same name. Your saved choice is unchanged.

In Ship now fills from the top down. The first turn sits at the top of the pane and the empty space is below the last turn, until the conversation is taller than the view. This applies to the window, the headset overlay and the mini overlay.

Ask "have I been to Lave?" and D47 says whether you have, how many times, and the day of your last visit, read from the list of visited systems Elite keeps on your PC for the Commander you are playing. A system missing from that list is answered as no record of a visit, not as never visited. Describing a system you have visited now ends with the same sentence. Your current system can be checked with galaxy lookups off. Any other system needs them on, and its name goes to Spansh to be found, as it does when you ask D47 to describe that system. The list itself is read on your PC and never sent.

D47 can look at your screen when you ask about something on it, such as "what's on my scanner". Turn on Let the model look at the screen in Settings, under the language model, or say "turn on screen pictures". It takes one picture of Elite's window, or of the headset's view when Elite is running in VR, and sends it to your language-model provider with that question only. The picture is never saved. The line under the reply says PICTURE OF ELITE'S WINDOW or PICTURE OF HEADSET VIEW, and the cost shown includes the picture. The setting is off by default, the model cannot turn it on, and Privacy and egress lists it as Screen pictures. When the model in use does not read pictures, the row says so and no picture is taken.

With Background model unset, In Ship chatter, narrator lines, the opening brief, lore lookups and voice choice use Claude Haiku 5.5 on Anthropic's own endpoint. A custom endpoint, or a Background model you set yourself, is unchanged. D47 tells you once and offers to keep your conversation model for these calls.

Claude Haiku 5.5 is in the model list, priced at $0.10 per million input tokens and $0.50 per million output tokens for prompts up to 100,000 tokens. Longer prompts cost five times that, which the spend estimate does not yet include.

Claude Fable 5.1 is in the model list, at $10 per million input tokens and $50 per million output tokens.

OpenAI's default model is now GPT-6.1 Sol, at $2 per million input tokens and $10 per million output tokens. GPT-6 Sol, GPT-6 Astra and GPT-6 Luna are in the model list too. With OpenAI selected and Background model unset, the background model is now GPT-6 Luna, at $0.10 per million input tokens and $0.50 per million output tokens.

The spend estimate now prices cached input correctly for Claude Haiku 5.5 and Claude Sonnet 5.5. It had shown Haiku 5.5's cache cost at a tenth of the real price and Sonnet 5.5's cache reads at twice the real price.

Claude Sonnet 5.5 now searches for the tools it needs instead of receiving every tool up front, as the other current Claude models already do.

A reworded callout in which the model says what it was or was not given, or that it cannot address you a certain way, is no longer spoken; the authored line is used instead.

The carrier tower now addresses you by rank and surname when it rewords a line, instead of saying it does not have your surname.

The Local voice and Chatterbox voice rows in Settings no longer show a Download it button once the model is installed. The button returns if any file is missing or the wrong size, and goes when a download finishes.

Chatterbox lists its twelve voices in an installed or updated D47, and the speaker portraits arrive with an update. Before, the installer's folder for voices and the updater's zip left them out.

Push-to-talk and callouts no longer wait while D47 writes to its log files.

Cartesia voices now use Sonic 3.6, because Cartesia retires Sonic 2 on October 20.

"How do I" no longer offers Journal reminders for a question about your journal or session, and help no longer suggests "noted" or "remind me next session" as a way to reach a feature. Those answers work only while a reminder or carrier warning has just gone off.

If D47 cannot reach Windows' audio devices at startup, the log now says so and why. Before, the speaker and microphone lists were empty and the log said nothing.

Beginning a story again, or editing its start time in adventures.json, no longer holds up push-to-talk and callouts while D47 re-reads your journals to work out where the story stands. Beginning a story, or moving its start time to after the newest event D47 has read, no longer re-reads your journals at all. Any other start-time change re-reads them in the background. Until it finishes, the Adventures tab shows the standing from before, and a beat you reach in the meantime is said once the re-read is done.

In the twelve hours before the Powerplay cycle ends, D47 no longer reads its view-state file ten times a second while you are pledged. It reads it once. The cycle line is still said once per cycle, including after a restart.

Push-to-talk and callouts no longer wait while D47 saves a story's progress or a story rating to story.json.

Saving a plotted route or hearing your mail no longer holds up push-to-talk and callouts while D47 writes the file.

When the Windows default speaker or microphone changes, push-to-talk and callouts no longer wait while D47 closes the old device and opens the new one.

Switching Commander, or cores waking, with the Log file page open no longer refreshes the per-subsystem level track from the tick thread.

Push-to-talk and callouts no longer wait while D47 searches the Elite install folders for your bindings after you change them in the game or switch preset. The search runs in the background, and D47 keeps using the bindings it had until it finishes.

Push-to-talk and callouts no longer wait on Windows' game-controller service, or while D47 updates the track shown in the Windows media flyout. D47 now reads your controllers on a separate thread every 50 milliseconds.

D47 could stop speaking and stop answering push-to-talk until restarted when one sound ended at the moment another started, or while the output device changed. Neither hangs it now.

When the language model rewrites a story's next objective, push-to-talk and callouts no longer wait while D47 saves the rewritten story to adventures.json.

When the Guardian cores wake, or D47 tells you what a story's cast needs before it can speak, push-to-talk and callouts no longer wait while D47 saves the message to Messages.

D47 no longer lists every file in your journal folder ten times a second while it follows the journal. It lists the folder only when the folder changes, so the time push-to-talk and callouts wait on it no longer grows with every session you play.

With Elite closed, say "save these bindings as sim pit" to keep a copy of Elite's control bindings under that name, and "load the sim pit bindings" to put them back; Elite uses them the next time it starts. A load first saves the bindings it replaces as "before last load". While Elite is running both are refused: "Elite is running. It reads its bindings only when it starts, so close Elite first and ask again." "List my binding profiles" names them, and Settings › Macros and switches lists each saved profile with a Delete button. None of this needs a language model.

While you sample organics on foot, D47 now says "Far enough for the next Stratum Paleas sample." once you are the genus' colony distance from every specimen of the set, measured along the surface. It is said once per specimen, and not at all without a surface position. Logging a different species while a set holds one or two specimens loses that set, and D47 says "That abandoned your 2 Stratum Paleas samples." as the scan lands. Approaching a body with biological signals whose scan shows nobody has walked it, D47 says "Nobody has set foot on Smojue EB-O d6-37 AB 1 b. First footfall bonus is there.", once per body. Settings > Callouts has three new switches, Far enough to sample, Abandoned samples and First footfall on approach, all on by default. The First footfall switch now also silences the line it names.

With a language model set up, the debrief's drafted directions are reworded by the model at the next launch, so "okay Warden no more speeches about the thargoids please" can read "Do not talk about the Thargoids unless asked." Each correction is sent once; your own words stay beside the draft, and the draft keeps them when the model says the line is not an instruction or the request fails. Settings › Privacy and egress says so under the language model.

On the Journal page, an Explain button sits beside Raw while the detail pane is shown. Select an event and press it: "Explain that" goes into the transcript and D47 answers about that event. It is disabled while nothing is selected or a reply is in progress. Saying "explain that" does the same with the pane folded.

Say "walk me through engineers", "show me how the checklist works" or "how do I use bookmarks" and D47 reads that help page's how-to steps aloud, one at a time. It says the page title and the number of steps, then the first step. "Next" or "skip" moves on, "again" repeats the step, "back" goes to the one before, "what should I see" says what the step should do, and "stop" or "cancel" ends it and cuts off the step being spoken. The cancel button and the shut-up hotkey also stop the voice mid-step and leave the walkthrough where it is. Anything else you say is answered as usual and the walkthrough waits where it is; ten minutes without one of these phrases ends it. Nothing is recorded.

The how-to steps on every capability page now each carry two lines written to be heard: what to do and what should happen. The documentation site does not show them. Activities, Mining, Bookmarks and Learned phrases gain a how-to band, and Clock, Switch Commander and Reminders gain steps.

The carrier captain's fuel and upkeep warnings answer to the same phrases as a journal reminder, whichever went off last. "Noted", "got it" or "thanks" keeps the warning quiet until its condition clears and comes back. "Remind me next time" lets it speak at the next dock or jump request on the same reading. "Remind me tomorrow" or "remind me next session" keeps it quiet until your next session. These are remembered only until D47 restarts.

When a journal reminder has gone off, say "noted", "got it" or "thanks" to remove it, "remind me next time" to arm it again on the same moment, or "remind me tomorrow" or "remind me next session" to arm it for the start of your next session. They answer the most recent reminder that went off, and are heard only while one has. Tomorrow means your next session, not a time of day. Only your own words do this; the model cannot.

Say "switch to commander Kestrel Vane" or "switch to Kestrel Vane" to switch D47 to that Commander. Every Commander in the journals except the one D47 is showing has both phrases, and D47 answers "Switched to CMDR Kestrel Vane. I'll stay with this commander until you switch again." Only your own words, the title bar and the Commanders page switch Commanders; the model cannot.

When the journals hold more than one Commander, the title bar shows the current one as CMDR NAME ▼ beside HELP. It opens a menu of every Commander, with the time each was last detected and a tick on the current one; choosing another switches D47 to them. MANAGE COMMANDERS › opens the new Commander › Commanders page, which lists each Commander's ship, last location and last detected time, with SWITCH on each row and ✓ CURRENT on the one D47 is showing. The page is on the headset panel too; the title-bar menu is on the window only.

Say "spell a system", "spell the system" or "spell a destination" to open a keyboard titled "System to plot" and spell the name letter by letter, then Done. The name is plotted exactly as spelled. When a plot ends with no route, D47 now ends its answer with "Spell it?" and asks yes or no; yes opens the keyboard with the name it heard already in it. The keyboard opens on the headset panel while the overlay is showing, otherwise on the window.

Ask "tell me about LTT 7786", or "tell me about this system", and D47 gives the system's government, allegiance, economy, population, controlling faction and its state, Powerplay power and state, how many stations of each kind, and the date of Spansh's last report, then opens Search › System on that system. A name with no exact match is answered with up to three close names. Needs galaxy lookups on.

Say "interstellar factors search", "nearest black market" or "run the anarchy outbreak search" to run a ready-made galaxy search from your own system. There are eleven: raw, manufactured and encoded material traders, Guardian and human technology brokers, Interstellar Factors, Universal Cartographics, Vista Genomics, Black Market, Pioneer Supplies, and anarchy systems in outbreak. Each takes the phrases "{name} search", "run the {name} search", "nearest {name}" and "find the nearest {name}", and runs only while galaxy lookups are on.

Asking D47 to find stations can now filter by station type (Starport, Outpost, Surface port, Settlement, Fleet carrier or Megaship), the smallest pad, the distance from arrival, services such as Interstellar Factors, Black Market or Vista Genomics, the kind of material trader or technology broker, a module or ship sold there, the station's controlling faction, allegiance, government and faction state, and its system's power, Powerplay state, population, economy and whether it is colonised. Several services together find stations that have every one. Fleet carriers are left out unless asked for. The answer gives each station's system, distance, type, largest pad, distance from arrival, and which of the asked-for services it has. Security and minor faction presence cannot be searched on stations. "Where can I buy" questions are answered as before.

Asking D47 to find systems can now filter by the Powerplay power controlling them, their Powerplay state (Exploited, Fortified, Stronghold or Unoccupied), their population, and whether they are colonised. A population of 0 finds unpopulated systems. An answer to a power search names each system's power and its state. A power or state D47 does not know is refused with the list of ones it does.

Asking D47 to find planets, moons or stars can now filter by volcanism, atmosphere, surface gravity, surface temperature, whether the body is tidally locked, how far it is from the arrival star in light seconds, a raw surface material, and the Powerplay power and state of its system. A volcanism or atmosphere D47 does not know is refused with the nearest names. Asked for the bodies richest in a material, D47 looks at the 50 nearest that carry it and names the richest of those with each one's share, and says it chose among the 50 nearest. The answer names each body's value for what you asked about.

When the published default model changes for the language model provider you use, or for ElevenLabs when it speaks, the panel shows one notice, once. If you never chose a model, it says D47 now answers with the new one and offers to keep the old; if you chose one, it names the new default and offers to switch to it. Dismissing it, or using either button, records the default as told.

Say "accept the adventure", "change the adventure" or "reject the adventure" to act on a draft that is waiting for your yes, as Accept, Change something and Decline do. They act on the draft whose page is open, or on the only draft when none is open. With two or more drafts and none open, D47 says to open the one you mean and changes nothing. With no draft, the phrases are not heard.

Say "ask for an adventure" to open the Stories tab at the Ask form, with the entry for the brief open. Say what the story should be about, or press Done with it empty, and D47 asks with the reach, length and ship choice the form shows, as Go does. When no language model is set up or galaxy search is off, the form opens and D47 says which instead.

The Adventures tab no longer writes or edits an adventure by hand. Ask D47 for one instead. Adventures you wrote before still play.

Each hired pilot's row on Fleet › Crew has a picture, with Change picture and Use the default. It is kept for the pilot's crew id, so two pilots with the same name keep separate pictures, and it shows beside that pilot's turns on the Conversation page. A pilot you fire keeps their picture, and a rehire gets it back.

The Commander record has a picture of you under your name, with Change picture and Use the default. The picture is kept for the Commander flying and shows beside your turns on the Conversation page; a second Commander has none until they choose one. Until D47 has seen which Commander is flying, the buttons are disabled and the page says "Fly once with D47 running to set your picture." A file over 10 MB, or one that is not a picture, is refused and nothing is saved.

Asking "read my mail", "any mail" or "check my messages" says what the journal shows has arrived as inbox mail since you last asked: missions completed or failed, promotions, squadron promotions and community goal rewards. d47 cannot see the inbox itself, so the answer says the comms panel has the true count. Asking again says nothing new until more arrives. Promotions d47 already announced are left out.

The Model and Model for the quiet calls rows also offer Anthropic models released since the model list was published, and the ElevenLabs model row offers new ElevenLabs models. d47 asks Anthropic which models your key reaches at startup and whenever the provider, endpoint or key changes, and asks ElevenLabs each time it fetches the voice list. Each model the list does not name is added at the end, labelled "new — priced as unknown". A new Anthropic model gets no tool search and thinks with an effort level only when Anthropic says it supports adaptive thinking; a new ElevenLabs model is sent no delivery direction and no speaking rate. When the request fails, the rows offer the published list alone. Privacy and egress says both requests are made. The ElevenLabs model row is now a stepper rather than three segments, because its list can change while d47 runs.

d47 now fetches the published list of models, their defaults and their prices from GitHub at startup and every 24 hours, so a new model or a new default arrives without a new release and takes effect without a restart. A fetched list is used only if it reads in full and is newer than the one in use; it is kept in `data\model-catalog.json`, and a release with a newer list built in replaces it. The Language model settings have a new Fetch model updates row, on by default; off makes no request. Privacy and egress lists the request as Model updates.

Navigation has a Best cargo page, after Market. While you are docked it shows the station and its system, your free hold less limpets, and a Sell in field. Type a system and press FIND CARGO or Enter, or press ROUTE END, drawn when a route is plotted, to fill in the route's last system. The page lists every commodity here that sells at a profit there, best total first, with the station that pays most, the profit a tonne, the tonnes, the total and a bar against the best. Under the tonnes it says what limited them: HOLD, SUPPLY or DEMAND. A line names the saved trade route filters the search uses, and CHANGE FILTERS opens the Trade route page. PLOT plots a course to the destination, the same as asking by voice. Asking "what should I buy here to sell in" a system by voice shows on the page too, and the spoken answer still names two. The page says when you are not docked, when galaxy lookups are off, when nobody has reported the docked market's prices, and when nothing sells at a profit. It searches only when asked.

d47 now says the colonisation deadlines. When you claim a system it says you have 24 hours to deploy the beacon and four weeks to finish its primary port, and, when you are in no squadron, that a squadron of your own extends the exclusive claim window from thirty minutes to a day. It reminds you two hours before the beacon deadline, and at seven days and 48 hours before the primary port deadline, unless the beacon is up or the system's first construction site is complete. Claims from earlier sessions are read from your journals, and a reminder whose moment passed while d47 was off is said at the next start. When a construction site's depot reports, d47 says how many tonnes are left and about how many trips that is in this ship, once per site each session; the construction needs answer ends with the same line. The Callouts section has a new Colonisation deadlines row, on by default.

The Conversation page shows a picture beside each core's replies, and beside the Captain's and the Tower's callouts. The 12 cores, the Captain and the Tower (each as a man and a woman) ship with the install.

Asset Mgmt has a Construction page, between Carrier and Materials, on the window, in the headset and on the mini panel. It lists the construction sites still being built under Building, each with its system, how far built, when you last saw it, and how many commodities and tonnes are outstanding. Sites you have not seen for 14 days are listed under Not seen since, greyed, with the date. The newest current site is selected when the page opens. Beside the list, the selected site shows how far it is built, when you last saw it, what is outstanding and whether your carrier's count is reconciled. A site not seen for 14 days carries a warning that says how many days old its figures are and that docking there brings them up to date. Under that, Needs lists each outstanding commodity with what remains, what is in your hold, what is on your carrier and what is left to buy; a commodity your hold and carrier cover reads ✓ COVERED, an open carrier order reads ORDER OPEN, and an unreconciled carrier reads UNKNOWN and is left out of what to buy. Last market search shows the stations, lots and prices from the last where-to-buy answer for that site, what nothing in range sells, what is still short, and the cost of the whole plan. SEARCH MARKETS runs the same search as asking by voice; with “Look things up in the galaxy” off it is disabled and the page says why. The page redraws as the journal moves the sites, the hold and the carrier.

The Conversation page can show each speaker's picture as a 44 px square beside their turn: left of the ship, the carrier captain, the tower, the Narrator, a hired pilot or a story's cast member, and right of your own turns. The ship's picture is the core that answered, and earlier replies keep it after you switch cores. The captain's and tower's follow the gender of the voice they speak in, and show none on a voice with no listed gender. Your own is `commander.<your Frontier id>.png` in `data\pictures\`, and any picture there replaces the one that came with d47 or a story. A turn with no picture is drawn as before, and the mini panel shows none.

A construction site is now current for 14 days after you last saw it. With no site named, the construction needs answer is about the one current site, and asks which when several are current. When none is current it names the most recent site and the date you last saw it, and the site list gives older unfinished sites one line with how many there are and the newest date. The needs answer now takes your hold and your carrier off each commodity, where the carrier's count by commodity matched Elite's cargo total at its last check, and says the carrier's count has not matched since that date where it did not. A commodity with an open carrier order says its figure may be off by what other Commanders have traded, and one that another current site also needs says the stock is shared with that site. The where-to-buy search is now for what is left after your hold and carrier, and leaves out commodities with nothing to buy.

An open rank goal on the Checklist's Goals view — the five careers, the Imperial and Federal Navy, and Powerplay — now has an Ask D47 button beside Remove. It sends a question such as "What's the fastest way to gain Mercenary rank from where I stand? Search the web if you can." as a typed question, and the answer appears in the transcript. A finished goal has no button, and pressing it while d47 is answering does nothing.

d47 now counts every commodity in your carrier's hold, not only tritium, from your own transfers, buys and sells at the carrier. Each time Elite reports the carrier's stats, d47 checks the count against the cargo total Elite gives, and an empty hold starts the count again. The tritium figure on the Carrier page now says "counted, may be off: the count did not match the hold" when the last check disagreed, as well as when a tritium order is open. Cancelling a tritium order leaves the figure marked as possibly off until the next check, since the order may have been partly filled. The count from your older journals is now kept when d47 starts, rather than only the count from this session.

Ask "what's something I haven't done in a while" and d47 names the three activities you have gone longest without, oldest first, with how long ago each was, with no language model. "When did I last go mining" gives the date in the galactic year and how long ago, or says the activity is not in your journals since they began. Say "don't suggest bounty hunting anymore" to leave an activity out of the suggestions, and "suggest bounty hunting again" to put it back; its date stays on the Activities page. Only you can change this, so the language model is refused and passes the phrase on.

The Checklist page has an Activities button beside Suggestions. It opens Checklist › Activities, which lists every activity d47 dates, oldest first, with the date you last did it in the galactic year and how long ago, or "Not in your journals since" the start of your journals for one that has none. Each row has a Suggest checkbox, and clicking anywhere on the row toggles it. An activity you untick is no longer suggested, and its row and date stay on the page.

Asset Mgmt › Ships has a new setting, **Hull viewer on the graphics card**, off unless you turn it on. On, the hull viewer on a ship's page and over the whole window is drawn through OpenGL with 4× multisampling instead of by the processor, so a large window turns smoothly. Turning it on or off changes the open viewer at once, at the same pose. It is best used with Elite not running, since the two share the graphics card. Where the window has no graphics card path, such as software rendering, the processor draws as before and the log says why, once a session.

The Checklist no longer holds a line for each delivery or collect mission, and no longer lists them when you ask for it. Missions are on Commander › Missions. A mission line already in your checklist is removed the next time the app loads it.

The on-foot engineers whose invitation asks for a number of missions, sales or settlement visits — Jude Navarro, Terra Velasquez, Eleanor Bresa, Rosa Dayette, Kit Fowler, Yarden Bond, Wellington Beck and Oden Geiger — now have that requirement counted. Counting starts at the first login whose engineer list names them, and includes your older journals. Once the count reaches the target the requirement is ticked with a full bar. Below it, the requirement stays unticked rather than marked unmet, with a bar and a line such as "2 counted since 4 Nov 2025", because missing journals and sales made before that login are not counted. Sales count at any bartender, your own carrier's included; Rosa Dayette's count only in Colonia, and Eleanor Bresa's visits count once per Colonia settlement.

d47 now knows the construction sites you have docked at in earlier sessions from the moment it starts, rather than only after you dock at one again. It reads them from your older journals with the rest of the history, and the newer reading of a site wins. What you have delivered to a site is still counted from this session only. A colonisation ship's name is now read as Elite shows it, "Apianus's Pride" rather than "$EXT_PANEL_ColonisationShip; Apianus's Pride", in the site, the transcript and the voice.

The Checklist now opens on your lists rather than one long list. Each list has a row under its group, Yours, Ships, Systems, Suits and weapons, or Nothing open, showing its name and kind, the next open line, and how many lines are open and done; the ship you are flying and the system you are in have their name in cyan. Choosing a row opens that list on its own page, Checklist › Gonzo for example, with its lines, ADD A LINE where you can add one, DELETE COMPLETED for that list only, and on a ship's list a checkbox for Only what engineers here do. The All lists row at the end opens every line together, each led by its list's name, with the filter and Delete completed for the whole checklist as before. Searching from the first page searches every list. The Goals checkbox and Suggestions are on the first page. The mini panel shows the first four lists and counts the rest.

Settings › Lore › Open your notes now shows what d47 itself knows about the system you are in, under "What D47 knows here" at the top of the dialog. It is the same text d47 says when you ask what is notable about the system, labelled "from d47's table", and has no Forget button. In a system d47 has no entry for, the dialog is as it was.

With Settings > Callouts > Your core's own subject on, three more cores remark on their own subject. Chart says the credits an hour from exploration data after you sell some, for example "900,000 credits an hour from exploration data this session." Mender says what repairs have cost this session, for example "300,000 credits on repairs this session", after a repair once the total passes 100,000 credits. Archivist counts the firsts this session after the third, for example "4 firsts this session: 2 undiscovered stars, 1 first footfall, 1 body mapped first", covering arrival stars nobody had discovered, first footfalls and bodies you mapped before anyone else. Each speaks at most once an hour, and Chart waits until the session is thirty minutes old.

With Settings > Hull pictures on, the Carrier page now shows your carrier's hull beside its tiles, at the Beside and Wide sizes, and clicking it opens the picture over the whole window as on a ship's page. It is the stock Drake carrier, fetched once from the same GitHub release and under the same setting as ship pictures. The mini panel and the squadron's carrier show none.

Settings › Learned phrases is now Settings › Phrases. You can add a phrase there: type a wording or a pattern such as "[boost|get clear] and [jump|engage]" in the SAY field, pick the phrase it stands for from the list, and press ADD or Enter. The page refuses what the voice refuses. A clash turns the field red and says which wording clashed and what it already stands for, with "BUILT-IN PHRASE · SHIP" or "YOUR PHRASE · <pattern>" under it; an empty field says NOTHING TO ADD and no phrase picked says PICK A PHRASE. Your phrases are listed first, newest at the top, each with a ✕ to forget it. Below them is every built-in phrase, grouped by capability, with one sentence saying what it does, and the search box filters both lists.

With Sentinel aboard and Settings > Callouts > Your core's own subject on, d47 now says what combat is earning: the credits an hour from bounties, combat bonds and vouchers, and where most of it came from, for example "1.2 million credits an hour in combat this session, the largest share from bounties." It speaks after a bounty, a combat bond or a cashed voucher, once the session is thirty minutes old, at most once an hour, and from the second time on also says the rate it gave last time. Kills are announced as before.

With Quartermaster aboard and Settings > Callouts > Your core's own subject on, d47 now names a community goal you have not joined, once per goal per session: its title, where it is, the time left and the top tier's reward where the journal gives one, for example "The Rescue Goal community goal at Galileo, Sol has 30 hours left. The top tier pays: Bonus credits. You have not joined it." Only goals with at least 24 hours left are named, and a goal whose time and reward are both unknown is not. It reads the goals the journal has reported; nothing new is sent anywhere.

A double-click on a slot row now plans it. On a ship's page, double-clicking a slot, or on a suit's or weapon's page the Grade or a mod row, opens the same prompt as **Plan this slot** or **Change the plan** on that slot's page. It needs room for the list and the slot page side by side, and works from the second double-click on a page, since the first click opens the slot page and moves the list. The headset keeps the button.

With a language model and personality on, asking about one engineer, such as "what does Liz Ryder ask for", now lets your core add one short remark about what their invitation asks for, in its own voice. The requirement is still stated as written, for example "Landmines ×200", and the remark may rest only on it. Each engineer gets at most one remark per session, and with personality off there is none. Answers that do not go through the language model are unchanged.

A ship's page now shows its Fuel, Hardness and Crew seats. The ship you are flying shows its live fuel level against its tank, such as "12.5 of 16 t", and follows Status.json as fuel is used. Any other ship with a remembered loadout shows its tank size alone, such as "16 t tank". Hardness and crew seats come from the hull table for every hull. In the module chooser, a frame shift drive's caption now adds its optimal mass and max fuel per jump, and armour and hull reinforcement add their hull boost with kinetic, thermal, explosive and caustic resistance, as the voice answers give them.

A utility mount can now say where it sits on the hull, for example "Utility Mount 3 (top, fore)" and "3 (top, fore)" in the Utility Mounts list. Positions are recorded by hand per hull; a mount with no recorded position is named as before.

Asking what to say for something now gets one plain sentence for each phrase it names, such as "Sets volume to 40." or "Reaches Landing Gear Down.", where an answer used to quote a tool's whole description. It also finds the phrases you taught yourself: asking about "drop the wheels" when you taught it for "gear down" names your wording and says what it does.

After the opening line at launch, d47 now says one line about your last complete session: where it ended, the ship, and the most notable rank, engineer, death, rebuy or interdiction in it, such as "Last session you finished docked at Garay Terminal, Deciat in the Python, and earlier you were promoted: Combat rank 5." With a language model and personality on, your core always says it in its own voice, which is one priced call per launch; otherwise the line is said as written. Nothing is said when there is no earlier session or nothing happened in it, or with a stock core aboard. Settings > Callouts has a new switch, Last session recap, on by default, and Privacy and egress has a new Last session recap entry saying what it sends: system, station and ship names and counts, without your Backstory or the active adventure.

You can now teach D47 a phrase by voice. Say "teach a phrase" or "new phrase" while flying, and D47 asks "What do you want to say?", then "What should it do?", then confirms: "'wheels out' will do 'drop the wheels'. Keep it?" Your first reply is taken word for word and not acted on, so a wording that is already a command does not run; one that answers a question, such as "yes" or "no", is refused and asked for again. The second must be a phrase D47 knows, or a pick from the "did you mean" it offers; if nothing matches, D47 says "No phrase matches that". A yes stores the wording and it runs the phrase from then on. "Cancel" at any step ends it with nothing learned, and a wording that clashes with another phrase is refused with the reason.

While you are pledged to a Power, twelve hours before the Powerplay cycle ends d47 now says how many merits you have earned for it this cycle: "The Powerplay cycle ends in twelve hours. You have earned 1,029 merits for Li Yong-Rui this cycle." It is said once per cycle, even across restarts, and the count includes merits earned in earlier sessions of the same cycle. The cycle ends on the week boundary set under community goals, Thursday 07:00 UTC by default. The game state the model reads now carries the same count and the time left, so "how many merits have I earned this week" has an answer. Settings > Callouts has a new switch, Powerplay cycle, on by default.

Asking "am I wanted", "crime status", "my bounties", "my fines" or "what do I owe" now answers by voice: your legal state as Elite reports it and whether you are on foot, then the unpaid fines and bounties the journal records for each faction, on foot and on the ship you are flying, largest first. The written answer gives each faction's fine and bounty totals and the date of its newest crime; the spoken one names three factions and counts the rest. Other ships carrying debts are counted. Notoriety is added with the time Elite wrote it, and left out until a Statistics event has been read. A fine the game never journaled is not in the ledger, and the answer says so.

A ship's page now opens with TALK THROUGH THIS BUILD, where you can ask D47 about the ship's plan. Type a question about a slot ("why that thruster"), ask for a critique ("is this build any good") or give a goal ("make it jump further"), and the reply appears on the page. A goal comes back as a proposal: one row per slot that would change, with the plan now, the plan proposed, a reason, and Accept and Reject, plus Accept all and Reject all. Above the rows is what the proposal costs with every change accepted: engineer ranks it is locked behind, and each material as held against needed. Nothing reaches the plan until you accept a row; accepting writes that slot as editing it yourself would, and the slot list, the checklist and the gap follow. A row whose slot you edited after the proposal says PLAN CHANGED SINCE and can only be rejected. You can also say the remark: it goes to the ship open on the Ships page, or the ship you are flying when none is open, and the spoken reply names at most three modules and points you to the page. A failed request says your plan is unchanged and offers Retry. The exchange is kept per ship until you press Clear, and is not kept when d47 closes. It needs a language model configured.

A ship's page now ends its details with a PLAN section when that ship has live engineering plans or an outstanding construction delivery. It lists each material the plans still need with what you hold, what is short (or "✓ MET") and a gauge, adds a notice for any need that takes more than one trip, names engineer ranks you have not reached, and groups missing materials that come from the same kind of place. It redraws as you collect materials.

Asking about one engineer or one of your ships now opens its page on the panel and in the headset, whatever they were showing. "Where is Felicity Farseer" opens her page under Asset Mgmt › Engineers, as does "what's left for Felicity Farseer"; "what am I flying" or asking about a ship by name opens that ship's page under Asset Mgmt › Ships. A question about several engineers, or a name d47 does not know, leaves the panel where it was, and a surface with a chooser open does not move. Callouts and other lines you did not ask for never move the panel.

A new Search tab, on the window and in the headset, opens on a System page showing a star system's record as Spansh last had it. It opens on the system you are in and follows you: each time you arrive somewhere new while the page is showing, it reads that system. Type any system name in the SYSTEM field and press Enter to open it instead; a name with no exact match lists up to five close names to choose from. A typed system stays until you press BACK TO MY SYSTEM. The page shows when the system was last reported to Spansh, its economy, government, allegiance, security, population, controlling faction and its state, whether it needs a permit, its main star, its coordinates and how many bodies it has, then each minor faction with its influence and its active and pending states. The page shares the "Look things up in the galaxy" setting and asks Spansh for nothing while it is off. The Galaxy search entry under Privacy and egress now says that the page, while open and following you, sends each system you arrive in to spansh.co.uk.

The System page gains a BODIES section beside OVERVIEW and STATIONS. It draws every star and planet in the system at once, a star first and then what orbits it, with each body's moons under it and indented one step per level. Each body shows its name, a LANDABLE tag where it applies, its type and its distance from arrival. A star's second line gives its mass, radius, class and whether it is scoopable. A planet's gives its mass, radius, gravity, temperature, pressure, volcanism, atmosphere, terraforming state and signal counts. Each ring has a line of its own with its type, reserve level and hotspots.

The System page gains a POWERPLAY section, completing the switch: OVERVIEW, STATIONS, BODIES, POWERPLAY. It shows the system's controlling power, state, control progress with a gauge, reinforcement, undermining and the powers present, or says the system has no Powerplay presence. Below that, NEARBY POWERPLAY SYSTEMS lists the Powerplay systems within 20 light years, or 30 for a Stronghold, nearest first, with each one's distance, state, controlling power and control progress. A filter narrows the list to Exploited, Fortified or Stronghold systems, each with its count, and the list says how many of the total it shows and that at most 100 are listed. Choosing a row opens that system on this page and stops the page following you, as typing its name does. The list is requested from Spansh the first time POWERPLAY opens for a system, not with every system you arrive in; a failed request shows its notice in the list and leaves the figures drawn.

Going into danger with unsold exploration data worth five million credits or more now gets a warning: "You are carrying 6.2 million credits of unsold data into a fight." on dropping into a war zone or a hazardous resource extraction site, and "Hull is failing with 6.2 million credits of unsold data aboard." when your hull falls below sixty percent. The value is your held surface maps plus analysed biology. Each of the three is said at most once every thirty minutes. Settings > Callouts has a new switch, Unsold data at risk, on by default.

Your active hired pilot now speaks when the fighter launches ("Fighter away. I'll stay on your wing."), docks ("Fighter's back in the bay."), is lost ("We've lost the fighter.") or is rebuilt ("Replacement fighter's ready in the bay."). If you launch the fighter and fly it yourself, the pilot says "I have the ship, Commander." Nothing is said with no pilot assigned. Settings > Callouts has a new switch, Fighter pilot, on by default.

When docking is granted at a Coriolis, Orbis, Ocellus or asteroid base, d47 now says where the pad is, four seconds after the station's own reply: "Pad ten, eight o'clock, halfway in." Six o'clock is straight below you as you fly in with the green lights on your right, and the depth is near the entrance, halfway in or at the back. Nothing is said at fleet carriers, outposts, surface ports or settlements, and the line is dropped if you dock or the request ends first. Settings > Callouts has a new switch, Landing pad, on by default.

A new, optional reminder says "You have been flying 4 hours, Commander." once when a session has run that long. A session starts when the game loads and ends when it shuts down, and loading again re-arms it. It is off by default. Settings > Callouts has a new switch, Session length, and a row beside it to choose 2, 3, 4 or 6 hours.

Arriving in a system where a faction you owe fines or bounties is present now gets a reminder: "You owe Sirius Inc 150 credits in fines on this ship." Debts from flying are kept for the ship you committed them in, and debts from on foot follow you into any ship. Every journal you have is read, so an old unpaid debt still counts, and paying a faction clears its debt. The largest debt is named, at most once an hour for each system. Settings > Callouts has a new switch, Unpaid fines and bounties, on by default.

Asking what your plans still need now says how many trips a storage cap forces: "your plans need 330 and you can only hold 300. That is at least 2 trips whatever happens."

A community goal you have joined now gets one warning when it has eight hours or less left: "The Hope of the Void community goal ends in 7 hours." It is said once for each goal until d47 restarts, and not for a goal that is complete, expired or not joined. Settings > Callouts has a new switch, Community Goal expiry, on by default.

Undocking after accepting missions at that station now gets one sentence on the board: "Two missions on the board. The tightest is Deliver Polymers, to Wadjuk Port, 13 hours left." With one mission it says "One mission on the board: Deliver Polymers, to Wadjuk Port, 13 hours left." The mission named is the unexpired one due soonest. Nothing is said on an undock with no new accept, or with the Missions callout switched off.

The Commander tab has a MISSIONS page, second after CHECKLIST, in the window and the headset. On the left are your accepted missions in the order the spoken mission board uses, under EXPIRING WITHIN THE HOUR, HAND IN HERE and EVERYTHING ELSE. Each row shows the mission's name, where it hands in, the time left and the reward; the time changes to the warning colour under an hour and reads EXPIRED once it has passed. Choosing a row shows its detail on the right: time left, reward, when you accepted it, where it hands in, who gave it, the faction it is against, its cargo or passengers, and how much has been delivered where the cargo depot reports it. PLOT TO HAND-IN plots a course to the hand-in system, and the glyph beside it copies the system name; a mission that hands in where you are docked says HAND IN HERE instead. The header line counts your missions, how many hand in here and the total reward. The search box keeps the missions whose name, faction, station or system matches. A mission accepted before your oldest journal shows only its time left. With no missions the page says so.

The System page gains a STATIONS section beside OVERVIEW, which lists every station in the system, nearest first, with its type, controlling faction, government, economy, largest pad and distance from the arrival star. A sidebar narrows the list to one kind (Starport, Outpost, Surface port, Settlement, Fleet carrier or Other) and to stations that offer every ticked service, from Commodity market to Rearm; a service no station in the system offers cannot be ticked. While STATIONS is open, the field on the title line finds a station by name. The Overview gains STATIONS BY KIND, a tile per kind with its count, each opening STATIONS on that kind. Switching sections and filtering ask Spansh for nothing.

Speech settings, under Narrator Voice, now has six rows for crew voices: First officer, Helm, Comms, Science officer, Security officer and Navigation. Each is picked from your ship's voice provider and applies to that role on every ship; left empty it reads "Same as other crew". A single seat can also hold a voice of its own, which applies only while that provider speaks for your ship; you pick it on Fleet › Crew.

Fleet › Crew has a new section, "Seats aboard" followed by the name of the ship you are flying, with one row for each seat the hull has: three on an Anaconda, none on a Sidewinder, where it says "The Sidewinder has no seat besides yours." Each row takes a role (First officer, Helm, Comms, Science officer, Security officer, Navigation, or Custom with a title of your own), a name and a voice, with SAVE and CLEAR. OFFER THE DEFAULTS fills every empty seat with a role and name suited to the hull and leaves filled seats alone. A save is refused, with the reason on the row, when the name is already a seat on that ship, a hired pilot, the ship AI or the carrier captain, or when two seats hold the same standard role. Seats are kept per ship in `data\crew-seats.json`. The hired pilots keep their own section, headed as the game's.

A filled seat on the ship you are flying now speaks the callouts in its role's area, under its own name, and the ship AI speaks the rest. The First officer takes checklist, mission, promotion, rebuy, community goal and trading mode callouts; Helm takes fuel and limpets; the Science officer takes discoveries, biology, mapping, emissions, footfalls, sampling, materials and prospecting; the Security officer takes danger, attack, kill and rival territory warnings; Navigation takes route, long jump and arrival callouts. A warning keeps its urgency and its alert sound whoever says it. When a role has no seat, or you switch to a ship without seats, the ship AI says those callouts again. A Custom seat takes no callouts.

With a First officer seat filled on the ship you are flying, asking for the mission board is answered by that seat, under its name and in its voice, with the same words the ship AI would have said. This applies when d47 answers without the model, as it does for "mission board" and "read the mission board"; when the model looks up the board as part of a longer answer, the ship AI still gives it. "What's on the slate" is a new way to ask for the board.

The Commander tab's sub-tabs now read Checklist, Standing, Statistics, This session, on the window and in the headset, and the tab opens on Checklist. A root you last had open is still restored.

Mapping a ring with the Detailed Surface Scanner now says its hotspots, most first, at most three, with the rest as a count: "A Ring: 6 Monazite, 4 Platinum, 3 Serendibite, and 2 more." With a mining target set, the target leads when the ring has it: "A Ring: 4 Platinum. Also 6 Monazite, 3 Serendibite, and 2 more.", or "A Ring: no Void Opal. 6 Monazite, …" when it does not. A ring is said once per session. Settings > Callouts has a new switch, Ring hotspots, on by default.

With an AI configured, you can ask how many limpets a haul takes, such as "how many limpets for ten tonnes of osmium". The answer comes from your own finished mining runs, read from your older journals at startup, and is rounded up to whole limpets: "About 2 collectors and 4 prospectors for 10 tonnes, from your 36 past runs of any material: a collector every 5.7 tonnes, a prospector every 3.3 tonnes." When a material was the one refined most in at least three runs, those runs' ratio is used and the answer names the material; otherwise it says it used all your runs. With no past runs it says "I have no mining runs to estimate from.", and while the journal history is still being read it says so and gives no number.

A mining run that ends on docking with at least one refined tonne is now summarised: "Mining run: 108 tonnes in 1 hour 11 minutes, about 91 an hour. 90 Platinum, 12 Samarium, 6 Praseodymium. 33 prospectors and 19 collectors." The rate runs to the last refined tonne, at most three materials are named with the rest as a count, and cores found are added. A run that ends in a death, or refined nothing, says nothing. Once per run, when the hold has no free space, D47 also says "Hold full. 108 tonnes refined this run." and says nothing with no run open. Settings > Callouts has two new switches, Mining run summary and Hold full while mining, both on by default.

You can now set a mining target: say "mining target painite", or "mining target platinum above twenty five percent" with an AI configured. While it is set, prospector results name only that material and judge the rock: "Platinum, 31%. Above your target.", "Platinum, 18%. Below your target.", or "No platinum." With no percentage, the line is the material and its share, with "Best you have found this session" as before. "Clear the mining target" removes it. The target is kept per Commander in `data\mining.json`. A prospected rock that has already been mined from now ends its line with "Already mined", with or without a target.

A carrier route that goes more than 1,000 light years from both Sol and Colonia now ends with an offer of an expedition kit for your checklist, one proposal per line, each accepted or declined on its own. Nothing is added until you accept a line. The lines cover a fuel scoop for each ship stored at the carrier without one, a tritium mining kit when no ship there is fitted to mine, AFMU and repair limpet controllers, an SRV bay and spare SRVs when no ship there has a bay, a Detailed Surface Scanner for each ship there without one, and limpets on the carrier market. You tick them by hand.

When the carrier's tritium warning fires and the stored plan for that carrier is past its last restock stop, the warning now names up to three pristine waypoints with an icy ring, nearest first, and the ships you have fitted for mining, or says none is.

Plotting a carrier route now also says where tritium is sold within one jump (500 light years) of the destination. A station that sells it is named with how long ago it was reported. If only carriers list it, the answer says so, at carrier prices, and that those listings move; if nothing does, it says that. A one-way plot with no station supply adds "Plot it as a round trip and carry the whole total." Asking for the nearest station selling a commodity, when no station does, now searches again with carriers and labels the result "carriers only, at carrier prices". The Galaxy search entry under Privacy and egress now says a carrier plot asks api.ardent-insight.com where tritium is sold around the destination.

Asking which of your ships are fit for mining lists the ones carrying a mining laser or Volley Repeater, a collector controller and a refinery, naming those modules. Ships missing one are named with what they lack, such as "no refinery", and a ship whose fitted modules were never read is reported as not seen fitted.

The Navigation tab's Plan page has a Carrier Route card for your own fleet carrier. Fill in To; From defaults to the carrier's system, and "Come back to the start" is on by default. There are no fields for the tank, hold tritium or used capacity: they are read from the carrier, as with "plot my carrier to Colonia". The plotted route is headed "Carrier route", with the total tritium and when carrier management was last read, and lists each jump with its distance, the tritium it uses and what is left in the tank, marking restock stops with the tonnes needed and systems with a pristine icy ring. A carrier whose management panel has never been opened gets "Open carrier management once so I can read the hold." on the card. Fleet › Carrier has a "Plan a carrier route" button that opens the card with From set to the carrier's system.

A planner on the Plan page that answers without plotting, such as a refusal or "No route from…", now keeps that answer on its card. It was cleared as soon as it appeared.

A stored carrier plan now follows your carrier. It moves forward when the carrier's own location or jump event reaches the next system, matched on the carrier's id. Your own ship jumping through a waypoint system, and a squadron carrier arriving there, do not move it. The plan can say how many jumps remain and whether the next stop needs a tritium restock.

D47 can now hold reminders for a moment in the game: your next docking, docking at a named station, arriving in a named system, docking at your own carrier, an empty or full hold, a full material, or your next session. Each is said once when the journal reaches that moment, as one sentence about the moment followed by your own words exactly as you gave them: "You asked me to remind you when you docked. Buy limpets before you leave." Your words are never sent to a language model. Reminders are kept per Commander in `data\journal-reminders.json`, and one that has been said is removed at the next game load. Settings > Callouts has a new switch, Reminders, on by default; with no reminders set it says nothing.

You can set those reminders by voice or by typing, with no AI configured: "remind me to buy limpets when I next dock", "remind me to sell data when I arrive in Sol", or with the moment first, "when I'm back at my carrier, remind me to fit the new shields". D47 reads each one back with the station, system or material name as it stored it: "I'll remind you to buy limpets when you next dock." "What reminders do I have" lists them, and "cancel the reminder to buy limpets" removes one. Only you can set or cancel a reminder; the AI is refused both. "Remind me to" with a moment the game does not record is declined, and the reply lists the moments it can use. A time such as "in twenty minutes" is declined too, because these reminders follow the game rather than the clock. In a run started with timers and alarms, a time goes to the timer and alarm tools instead.

Saying "plot my carrier to Colonia" plots your own fleet carrier's jumps there and back, using the tank, the tritium in its hold and the used capacity from the last time carrier management was opened. The answer gives the number of jumps, the tritium for the whole journey, up to three restock stops by name and a count of the rest, how many waypoints have a pristine icy ring to mine from, and how old the management reading is. Say "one way" to leave out the trip back. A squadron carrier, or a carrier whose management panel has never been opened, is refused. The Galaxy search entry under Privacy and egress now says a carrier plot sends the start, the destination, those three figures and the carrier's capacity to spansh.co.uk.

Saying "explain that" or "explain that event" asks the language model about the event selected on the Journal page. The event goes to the model as Elite wrote it, except that the text of any message another player typed, or that you typed, is replaced with "[withheld: typed by a player]"; who sent it, on which channel and when are still sent. Where the Journal page has a paragraph for that kind of event, it is sent after the event, so the spoken answer and the paragraph agree. With nothing selected, the answer is "Nothing is selected on the Journal page." and nothing is sent. The Language model entry under Privacy and egress now says this.

The Journal page's reading now has a WHAT THIS CHANGED band between What this means and Every field, open by default, saying what d47 took from the selected event, such as "This event updated: docked at BNH-T2F". An event that changed nothing reads "Nothing in d47's picture changed.", which is a normal answer for most events, and an event that never reached your state has no band. Folded, the band shows the receipt as a one-line preview, and selecting another event opens it again.

Pledged to a Power, Directive 47 now warns when you start a hyperspace jump with salvage you scooped in your own Power's system and have not handed in, because it earns reinforcement merits only when handed in at a Power contact in that same system. It names up to three kinds and counts the rest, and says nothing about escape pods, which earn no merits. The new Powerplay salvage row under Callouts turns the warning off.

Pledged to a Power, Directive 47 now says which way the merit modifier leans the first time you enter normal space in one of its systems in a session: reduced merits for reinforcing your own Power's quiet system, better pay once rivals have undermined it, fifteen percent more for undermining a rival's. It says nothing in an unoccupied system. Asking "what's the merit multiplier here" is answered with the figures, with the galaxy map's marked-system bonuses given as "if" because the journal does not say which systems are marked. The new Powerplay merits row under Callouts turns the spoken line off.

The Set aside button on a goal is now Remove. Below the goals, a Removed list names each removed goal with a Recover button that puts it back where it was, and is hidden when nothing is removed. By voice, "remove the mercenary goal" and "recover the mercenary goal" do the same. Goals you had already set aside appear in the Removed list.

ElevenLabs, OpenAI, Cartesia and Edge speech now starts playing while the rest of it is still arriving, so the first words of an answer come sooner. This applies to a voice with no treatment on it and to the stock COVAS voice with its reverb, which now starts sooner on ElevenLabs as well. Voices heard over comms, including a story character on a link, now start the same way, so a hailed NPC answers sooner. The link's level control now adjusts as the voice plays instead of measuring the whole line first, so loudness can shift slightly within a line; the Guardian helmet and hologram effects, which use the same link, change the same way. Guardian voices start sooner too: a Guardian core whose effects all work as the voice arrives, such as the Damaged core preset, and a story character with such effects, start playing before the sentence group has fully arrived. The Guardian level adjusts as the voice plays, so loudness can shift slightly at the start of a line. An effect that needs the whole sentence, such as Stutter or Whisper, still waits for it.

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

The Fleet tab has a new page, Crew, after Stored modules. It lists each pilot you have hired with their combat rank, ON FIGHTER DUTY in yellow or OFF DUTY in grey, and the ship they were assigned aboard, or NOT POSTED. It updates when you hire, fire or reassign a pilot. Asking for help with crew now says it has a page on the panel called Crew.

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

Click any speaker picture to see it full size over the window. This works on the Transcript's turns and on the pictures in the Story log, the Stories cast list, the Crew page and Standing, in the main window and on the headset big panel. **Close** or Escape puts it away; on the headset **Close** is the way out. A picture is never drawn larger than its own size and is shrunk to fit a small window.

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
that get humor, and d47 decides line by line. Up to level 6 the humor has no puns,
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
