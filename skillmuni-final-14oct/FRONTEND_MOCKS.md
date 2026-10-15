# SkillMuni frontend contract · mock backend

The MSW mock handlers (`src/mocks`, 14 Oct 2026, final): the behaviour, status codes, error codes and sample data the real services must reproduce. Mock-only helpers (persona, scenario params, sessionStorage) are demo plumbing, not rules. Where a mock and the BRD differ, the BRD wins.

## `src/mocks/arena.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { sceneOr } from './persona';

/**
 * The Arena (Figma Leaderboard v2 52:172). Sample data from the design (handoff 58:1530 flags streaks,
 * movement and leagues as "to confirm" in the backend). `?arena=empty` (no XP yet this week) and
 * `?arena=error` in the page URL show those states. "All time" puts you at #9 to show the pinned row.
 */
interface Seed {
  id: string;
  name: string;
  xp: number;
  streak: number;
  modules: number;
  movement: number;
  title: string;
  level: number;
  badges: number;
  skill: string;
  cheers: number;
}

const P = (
  id: string,
  name: string,
  xp: number,
  streak: number,
  modules: number,
  movement: number,
  title = 'Rookie',
  level = 1,
  badges = 2,
  skill = 'English Vinglish',
  cheers = 12,
): Seed => ({ id, name, xp, streak, modules, movement, title, level, badges, skill, cheers });

const YOU = 'me';

const BOARDS: Record<string, Seed[]> = {
  week: [
    P('ananya', 'Ananya Iyer', 183, 9, 6, 0, 'Pathfinder', 4, 7, 'Brain Vita', 31),
    P(YOU, 'Yash', 68, 5, 3, 1, 'Explorer', 2, 4, 'Brain Vita', 8),
    P('kabir', 'Kabir Nair', 41, 0, 2, -1, 'Explorer', 2, 3, 'Banking', 5),
    P('priya', 'Priya Deshmukh', 14, 2, 2, 1),
    P('vihaan', 'Vihaan Gupta', 4, 0, 1, -1, 'Rookie', 1, 1, 'IT Gyan', 2),
    P('meera', 'Meera Joshi', 2, 1, 1, 0, 'Rookie', 1, 1, 'Hospitality', 1),
  ],
  month: [
    P('ananya', 'Ananya Iyer', 612, 9, 14, 0, 'Pathfinder', 4, 7, 'Brain Vita', 31),
    P('kabir', 'Kabir Nair', 455, 0, 11, 1, 'Explorer', 2, 3, 'Banking', 5),
    P('priya', 'Priya Deshmukh', 390, 2, 9, -1),
    P('rohan', 'Rohan Mehta', 301, 4, 8, 2, 'Explorer', 3, 5, 'Sharp Shooter', 9),
    P(YOU, 'Yash', 288, 5, 7, 1, 'Explorer', 2, 4, 'Brain Vita', 8),
    P('vihaan', 'Vihaan Gupta', 120, 0, 3, -2, 'Rookie', 1, 1, 'IT Gyan', 2),
    P('meera', 'Meera Joshi', 96, 1, 2, 0, 'Rookie', 1, 1, 'Hospitality', 1),
  ],
  all: [
    P('ananya', 'Ananya Iyer', 2140, 9, 31, 0, 'Legend', 7, 15, 'Brain Vita', 31),
    P('rohan', 'Rohan Mehta', 1870, 4, 27, 0, 'Pathfinder', 6, 12, 'Sharp Shooter', 9),
    P('sara', 'Sara Khan', 1602, 3, 22, 1, 'Pathfinder', 5, 10, 'English Vinglish', 14),
    P('kabir', 'Kabir Nair', 1499, 0, 21, -1, 'Explorer', 4, 9, 'Banking', 5),
    P('priya', 'Priya Deshmukh', 1310, 2, 19, 0, 'Explorer', 4, 8),
    P('dev', 'Dev Malhotra', 1205, 6, 18, 2, 'Explorer', 4, 7, 'IT Gyan', 6),
    P('isha', 'Isha Rao', 1102, 1, 16, -1, 'Explorer', 3, 6, 'Hospitality', 4),
    P('aditya', 'Aditya Singh', 990, 0, 15, 0, 'Explorer', 3, 6, 'Insurance', 3),
    P('tanvi', 'Tanvi Shah', 874, 2, 13, 1, 'Explorer', 3, 5, 'It’s My Style', 7),
    P('vihaan', 'Vihaan Gupta', 760, 0, 11, -1, 'Rookie', 2, 3, 'IT Gyan', 2),
    P('meera', 'Meera Joshi', 702, 1, 10, 0, 'Rookie', 2, 3, 'Hospitality', 1),
    P(YOU, 'Yash', 655, 5, 9, 2, 'Explorer', 2, 4, 'Brain Vita', 8),
  ],
};

const cheered = new Set<string>();
const cheerDelta = new Map<string, number>();

function scenario(): string {
  return sceneOr('arena', 'empty');
}

const initials = (name: string) =>
  name
    .split(/\s+/)
    .map((w) => w[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

function filter(scope: string, seeds: Seed[]): Seed[] {
  if (scope === 'friends') return seeds.filter((s, i) => s.id === YOU || i % 2 === 0).slice(0, 5);
  if (scope === 'batch') return seeds.filter((s) => s.id !== 'ananya');
  return seeds;
}

export const arenaHandlers = [
  http.get('/api/arena', async ({ request }) => {
    await delay(450);
    const mode = scenario();
    if (mode === 'error') return new HttpResponse(null, { status: 503 });
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'everyone';
    const period = url.searchParams.get('period') ?? 'week';
    const seeds = mode === 'empty' ? [] : filter(scope, BOARDS[period] ?? BOARDS['week'] ?? []);
    const players = seeds.map((s, i) => ({
      id: s.id,
      rank: i + 1,
      name: s.name,
      initials: initials(s.name),
      xp: s.xp,
      streak: s.streak,
      modulesDone: s.modules,
      movement: s.movement,
      you: s.id === YOU,
      title: s.title,
      level: s.level,
      badges: s.badges,
      topSkill: s.skill,
      cheers: s.cheers + (cheerDelta.get(s.id) ?? 0),
      cheered: cheered.has(s.id),
    }));
    const me = players.find((p) => p.you);
    const above = me ? players[me.rank - 2] : undefined;
    return HttpResponse.json({
      scope,
      period,
      players,
      me: {
        rank: me?.rank ?? 0,
        xp: me?.xp ?? 0,
        rankToday: me?.movement ?? 0,
        streak: 5,
        nextName: above?.name ?? null,
        nextXp: above?.xp ?? null,
        topXp: players[0]?.xp ?? 0,
      },
      week: ['done', 'done', 'done', 'done', 'done', 'today', 'upcoming'],
      promotionRank: 3,
      league: 'Gold League',
      rankUp:
        mode !== 'empty' && period === 'week' && scope === 'everyone'
          ? { to: me?.rank ?? 0, passed: 'Kabir' }
          : null,
      rewards: {
        coins: 640,
        voucher: { discount: '20% OFF', title: 'Food & café voucher', cost: 300, ready: 2 },
      },
    });
  }),
  http.post('/api/arena/players/:id/cheer', async ({ params, request }) => {
    await delay(250);
    const id = String(params['id']);
    const { on } = (await request.json()) as { on: boolean };
    if (on) cheered.add(id);
    else cheered.delete(id);
    cheerDelta.set(id, on ? 1 : 0);
    const base = Object.values(BOARDS)
      .flat()
      .find((s) => s.id === id);
    return HttpResponse.json({ cheers: (base?.cheers ?? 0) + (on ? 1 : 0) });
  }),
  http.post('/api/arena/players/:id/challenge', async () => {
    await delay(300);
    return new HttpResponse(null, { status: 204 });
  }),
];
```

## `src/mocks/assessment.data.ts`

```ts
/**
 * Career Drivers Survey 2025 (client document, 2026-10-06). Content the server owns: the 36 pairs,
 * the 9 drivers (A–I) and the 6 archetypes. Text is verbatim from the survey; the short "blurb"
 * lines and emojis are UX copy written for the results screens (A-212).
 */
import type {
  Archetype,
  DriverId,
  DriverInfo,
  Island,
  Pair,
} from '../app/core/assessment/assessment.api';

const p = (n: number, a: [string, DriverId], b: [string, DriverId]): Pair => ({
  n,
  this: { text: a[0], driver: a[1] },
  that: { text: b[0], driver: b[1] },
});

export const PAIRS: readonly Pair[] = [
  p(
    1,
    [
      'I aim for a lifestyle where I can comfortably afford the experiences and things I want.',
      'A',
    ],
    ['I want to have a strong voice in decisions and influence outcomes that matter.', 'B'],
  ),
  p(
    2,
    ['I need to feel that my work contributes to something meaningful and worthwhile.', 'C'],
    ['I want to develop deep expertise in my field and be recognized for it.', 'D'],
  ),
  p(
    3,
    ['I want opportunities to innovate, design, or bring fresh ideas to life.', 'E'],
    ['It’s important for me to work in a supportive, collaborative environment.', 'F'],
  ),
  p(
    4,
    ['I value flexibility and autonomy to decide how I work and what I focus on.', 'G'],
    [
      'I want the stability and security of knowing my future is financially and professionally predictable.',
      'H',
    ],
  ),
  p(
    5,
    ['I take pride in being respected and recognized for my achievements.', 'I'],
    ['I want to enjoy the financial freedom that comes with success.', 'A'],
  ),
  p(
    6,
    ['I want to influence and guide others toward shared goals.', 'B'],
    ['I’m motivated by doing work that aligns deeply with my personal values.', 'C'],
  ),
  p(
    7,
    ['I want to master specialized skills and be seen as a go-to expert.', 'D'],
    ['I want to create original ideas or work that carries my personal signature.', 'E'],
  ),
  p(
    8,
    ['Building meaningful, friendly relationships at work is essential to me.', 'F'],
    ['I enjoy deciding how I structure my day and tasks.', 'G'],
  ),
  p(
    9,
    ['I’m motivated by being able to afford a high-quality lifestyle.', 'A'],
    ['I want to demonstrate to myself and others that I truly know my craft.', 'D'],
  ),
  p(
    10,
    ['My work should reflect my personal sense of purpose.', 'C'],
    ['I want the things I produce to clearly carry my name or identity.', 'E'],
  ),
  p(
    11,
    ['I want to afford whatever I need without financial stress.', 'A'],
    ['Having a stable, long-term career matters a lot to me.', 'H'],
  ),
  p(
    12,
    ['I enjoy leading, influencing, and mobilizing people.', 'B'],
    ['I would enjoy becoming a respected specialist in my field.', 'D'],
  ),
  p(
    13,
    ['It matters to me that my work makes a positive difference in the world.', 'C'],
    ['Having close, trusting relationships at work is very important to me.', 'F'],
  ),
  p(
    14,
    ['I want my personal creativity to shine through in what I do.', 'E'],
    ['I’d prefer to chart my own course rather than follow strict directions.', 'G'],
  ),
  p(
    15,
    ['Working closely with people I like gives me a special sense of fulfillment.', 'F'],
    ['Knowing that my future is financially and professionally secure is critical.', 'H'],
  ),
  p(
    16,
    ['I want to have enough money to live comfortably and pursue my interests.', 'A'],
    ['I want to be innovative in my work and take on new challenges.', 'E'],
  ),
  p(
    17,
    ['I feel energized when I’m guiding, directing, or leading others.', 'B'],
    ['I feel happiest when I’m surrounded by people I connect with.', 'F'],
  ),
  p(
    18,
    ['I see my career as part of something meaningful and bigger than myself.', 'C'],
    ['I like taking full responsibility for my decisions and direction.', 'G'],
  ),
  p(
    19,
    ['I’d love to be recognized as a leading expert in my domain.', 'D'],
    ['Feeling that my career path is stable and predictable makes me comfortable.', 'H'],
  ),
  p(
    20,
    ['A comfortable and financially strong lifestyle motivates me.', 'A'],
    ['Meeting and connecting with new people through work excites me.', 'F'],
  ),
  p(
    21,
    ['I enjoy being in positions where I can set direction for others.', 'B'],
    ['Choosing my own tasks and priorities is important to me.', 'G'],
  ),
  p(
    22,
    ['I’m willing to work hard if I believe the outcome truly matters.', 'C'],
    ['I’d feel reassured knowing where I stand financially in the long run.', 'H'],
  ),
  p(
    23,
    ['Strong bonds with colleagues make it difficult for me to leave a job.', 'F'],
    ['Being acknowledged as part of an influential community is important to me.', 'I'],
  ),
  p(
    24,
    ['I’d enjoy taking charge of teams, projects, or resources.', 'B'],
    ['I want to create or build something that’s entirely new.', 'E'],
  ),
  p(
    25,
    ['I prefer to do what I deeply believe is right, even if it doesn’t boost my career.', 'C'],
    ['Being publicly recognized for my contributions matters to me.', 'I'],
  ),
  p(
    26,
    ['I want to do something that sets me apart from others.', 'E'],
    ['I usually prefer the secure and stable option.', 'H'],
  ),
  p(
    27,
    ['I want others to look to me for leadership.', 'B'],
    ['Social prestige and status are strong motivators for me.', 'I'],
  ),
  p(
    28,
    ['A high standard of living motivates me.', 'A'],
    ['I want to avoid being tightly controlled by a boss or rigid system.', 'G'],
  ),
  p(
    29,
    ['I want the work I do to clearly reflect my personal identity.', 'E'],
    ['I value formal recognition and acknowledgment of my achievements.', 'I'],
  ),
  p(
    30,
    ['I prefer to be the one making decisions and taking charge.', 'B'],
    ['It worries me when my professional future feels uncertain.', 'H'],
  ),
  p(
    31,
    ['I want to be known as someone with valuable specialist knowledge.', 'D'],
    ['I would find satisfaction in working independently without constant oversight.', 'G'],
  ),
  p(
    32,
    ['I dislike feeling like just another number in a large system.', 'G'],
    ['I’d enjoy having a high-prestige position in my field.', 'I'],
  ),
  p(
    33,
    ['I’m prepared to work harder for better financial rewards.', 'A'],
    ['I see my work as a way to grow as a person.', 'C'],
  ),
  p(
    34,
    ['I’d like to hold a position that commands respect and visibility.', 'I'],
    ['I’m consistently drawn to roles with stable futures.', 'H'],
  ),
  p(
    35,
    ['When the people I work with are my friends, nothing else matters as much.', 'F'],
    ['Making expert contributions to my field gives me great satisfaction.', 'D'],
  ),
  p(
    36,
    ['I’d enjoy the recognition and respect that come with senior roles.', 'I'],
    ['I want to be highly competent in my specialization.', 'D'],
  ),
];

/** Figma B1 quest map: 4 islands of 9 pairs. */
export const ISLANDS: readonly Island[] = [
  { n: 1, name: 'Vibe Check', emoji: '🌅', from: 1, to: 9 },
  { n: 2, name: 'Main Character', emoji: '🎬', from: 10, to: 18 },
  { n: 3, name: 'Plot Twist', emoji: '🌀', from: 19, to: 27 },
  { n: 4, name: 'Final Form', emoji: '👑', from: 28, to: 36 },
];

export const DRIVERS: readonly DriverInfo[] = [
  {
    id: 'A',
    name: 'Material Rewards',
    emoji: '💰',
    focus: 'Financial growth, lifestyle, tangible benefits',
    definition:
      'Refers to the desire for financial security, wealth, and the ability to enjoy a high standard of living. People driven by material rewards make career choices that enhance their economic well-being. They seek roles with competitive compensation and are often willing to take on challenging or less fulfilling work if it provides significant financial returns.',
    blurb: 'Good pay and the lifestyle it buys keep you going.',
    strengthZones: 'Sales, high-commission roles, entrepreneurship, competitive industries',
    families: 'Sales & retail, commercial roles, high-income tracks',
  },
  {
    id: 'B',
    name: 'Power / Influence',
    emoji: '⚡',
    focus: 'Impact, decision-making, leadership',
    definition:
      'Reflects the motivation to shape decisions, direct people or resources, and play a central role in key choices. These individuals aim for positions where they can influence outcomes, set direction, and make important decisions. Confident and proactive, they thrive in leadership, strategic, or policy-making roles.',
    blurb: 'You want a say in where things go, and to lead people there.',
    strengthZones: 'Supervisory roles, team leads, managerial tracks, operations control',
    families: 'Leadership, operations, management tracks',
  },
  {
    id: 'C',
    name: 'Meaning / Purpose',
    emoji: '🌍',
    focus: 'Contribution, values, positive impact',
    definition:
      'Centers on being motivated by work that aligns with personal beliefs, values, or a sense of higher purpose. These individuals want to make a meaningful contribution through social impact, education, sustainability, or ethical work. Personal fulfillment matters more than short-term rewards.',
    blurb: 'Work has to matter. You want to make a real difference.',
    strengthZones: 'CSR, NGOs, climate action, education, public sector, mission-driven roles',
    families: 'Education, social impact, public sector',
  },
  {
    id: 'D',
    name: 'Expertise',
    emoji: '🎯',
    focus: 'Mastery, specialization, professional excellence',
    definition:
      'Reflects the desire to build deep knowledge and skills in a specific field. People with this driver focus on developing and maintaining specialist capabilities, preferring to work within their areas of strength. Their greatest satisfaction comes from being recognized as credible experts and staying current through continuous learning.',
    blurb: 'You want to master your craft and be the go-to person for it.',
    strengthZones: 'Technical specialists, analysts, engineers, finance experts, niche consulting',
    families: 'Specialist tracks, technical roles, finance, engineering',
  },
  {
    id: 'E',
    name: 'Creativity',
    emoji: '🎨',
    focus: 'Innovation, originality, self-expression',
    definition:
      'Represents the motivation to generate new ideas, solutions, or original work. Creative individuals thrive on solving problems in new ways, pushing boundaries, and bringing unique ideas to life, whether through product design, entrepreneurship, research, or artistic pursuits.',
    blurb: 'You bring fresh ideas to life and leave your signature on the work.',
    strengthZones: 'Product design, marketing, entrepreneurship, R&D',
    families: 'Innovation, design, marketing, entrepreneurship',
  },
  {
    id: 'F',
    name: 'Affiliation',
    emoji: '🤝',
    focus: 'Connection, belonging, meaningful relationships',
    definition:
      'Highlights the value placed on close, supportive relationships at work. People with this driver prioritize trust, collaboration, and human connection. They often choose to stay in roles because of the people they work with and derive a sense of purpose through relationships rather than hierarchy.',
    blurb: 'The people you work with make or break a job for you.',
    strengthZones: 'Customer service, teaching, people management, HR, hospitality',
    families: 'People-facing roles, HR, education, service',
  },
  {
    id: 'G',
    name: 'Autonomy',
    emoji: '🧭',
    focus: 'Freedom, flexibility, independence',
    definition:
      'Reflects the desire to control one’s time, decisions, and work methods. People with this driver thrive in environments that allow them to make their own choices, often avoiding rigid hierarchies and bureaucracy. They excel in roles like freelancing, consulting, or entrepreneurship where independence is valued.',
    blurb: 'You want control over your time, decisions and how you work.',
    strengthZones: 'Freelancing, entrepreneurship, field work, consulting',
    families: 'Freelancing, consulting, entrepreneurship',
  },
  {
    id: 'H',
    name: 'Security',
    emoji: '🛡️',
    focus: 'Stability, predictability, reduced risk',
    definition:
      'Focuses on valuing stability and a clear sense of the future. These individuals prioritize avoiding unnecessary risks and ensuring long-term reliability. They often seek employers known for stability and prefer well-defined career paths that offer predictability and peace of mind.',
    blurb: 'You like stability, clear paths and knowing where you stand.',
    strengthZones: 'Government jobs, large corporates, banking, administrative operations',
    families: 'Administrative, government, stable large organisations',
  },
  {
    id: 'I',
    name: 'Status',
    emoji: '🏆',
    focus: 'Recognition, prestige, social esteem',
    definition:
      'Reflects the desire to be acknowledged, respected, and admired by others. People with this driver are motivated by visible recognition: titles, awards, networks, or association with prestigious institutions. They actively build their reputation and seek opportunities that elevate their professional profile.',
    blurb: 'Recognition, respect and visible achievement.',
    strengthZones: 'Client-facing roles, leadership, public representation, luxury sectors',
    families: 'Client relations, leadership, public-facing roles',
  },
];

export const ARCHETYPES: readonly Archetype[] = [
  {
    id: 'people-first',
    name: 'The People-First Service Builder',
    emoji: '🤝',
    dominant: ['F', 'H'],
    secondary: ['C', 'B'],
    roles:
      'Customer service, client success, HR service, training, hospitality, community operations',
    redFlags: 'Solitary specialist roles with little people contact; high-risk startups',
    summary:
      'You do your best work with people you trust, somewhere steady. Best-fit roles: customer service, client success, HR service, training, hospitality and community operations.',
  },
  {
    id: 'rainmaker',
    name: 'The Commercial Rainmaker',
    emoji: '💼',
    dominant: ['A', 'I'],
    secondary: ['B', 'G'],
    roles: 'Retail & B2C/B2B sales, business development, key accounts, luxury, revenue leadership',
    redFlags: 'Low-upside admin roles; back-office analyst work with no customer exposure',
    summary:
      'You chase big wins and like being seen for them. Best-fit roles: retail and B2B sales, business development, key accounts, luxury and revenue leadership.',
  },
  {
    id: 'specialist',
    name: 'The Specialist Operator',
    emoji: '🔧',
    dominant: ['D', 'H'],
    secondary: ['G'],
    roles: 'Technical specialist, finance/ops analyst, engineering, compliance, process excellence',
    redFlags: 'Highly ambiguous “build-as-you-go” creative roles; pure sales',
    summary:
      'You go deep, get it right and like a clear path. Best-fit roles: technical specialist, finance or ops analyst, engineering, compliance and process excellence.',
  },
  {
    id: 'pathfinder',
    name: 'The Creative Pathfinder',
    emoji: '🌈',
    dominant: ['E', 'G'],
    secondary: ['I', 'A'],
    roles: 'Product/design, marketing/content, R&D/innovation, creative consultancy, founder',
    redFlags: 'Rigid bureaucracy; narrow repetitive tasks with no room for ideas',
    summary:
      'You light up when you can create, try new things and own how you work. Best-fit roles: product and design, marketing and content, R&D, creative consultancy, founding something.',
  },
  {
    id: 'purpose',
    name: 'The Purpose-Driven Community Leader',
    emoji: '🌱',
    dominant: ['C', 'F'],
    secondary: ['D', 'B'],
    roles: 'Education/edtech, NGOs, climate/sustainability, public service, L&D, social programs',
    redFlags: 'Purely money/status-driven cultures; hard-sell commercial roles',
    summary:
      'You want your work to matter and to bring people with you. Best-fit roles: education and edtech, NGOs, climate and sustainability, public service, L&D and social programmes.',
  },
  {
    id: 'strategist',
    name: 'The Strategic Leader',
    emoji: '👑',
    dominant: ['B', 'I'],
    secondary: ['A'],
    eitherOf: ['D', 'G'],
    roles:
      'Senior leadership, strategy, policy, business unit heads, organizational shapers, entrepreneurial ventures',
    redFlags:
      'Roles with low autonomy, low visibility, or purely operational with no decision-making scope',
    summary:
      'You want to set direction and be the one people look to. Best-fit roles: senior leadership, strategy, policy, business unit heads and entrepreneurial ventures.',
  },
];
```

## `src/mocks/assessment.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { creditXp } from './xp-ledger';
import { sceneOr } from './persona';
import type {
  Answer,
  AssessmentResult,
  AssessmentState,
  DriverScores,
} from '../app/core/assessment/assessment.api';
import {
  isAnswered,
  matchArchetype,
  rankDrivers,
  scoreDrivers,
} from '../app/core/assessment/scoring';
import { ARCHETYPES, DRIVERS, ISLANDS, PAIRS } from './assessment.data';

/**
 * Career Assessment v2 · Muni's Balance (Figma 259:19781). Scoring runs "on the server" with the
 * same pure functions the tests cover. State lives in sessionStorage so a reload resumes.
 * Page URL scenarios (`?ca=`): new · resume (13 answered) · final (34/36, 2 skipped) · done
 * (3 attempts, S1) · error. Submitting while the browser is offline fails like a real network (S3).
 */

const KEY = 'sm_mock_assessment';
const XP_ISLAND = 25;
/** A-309: 25 XP per island, 100 in all; the last island's 25 is paid on submit. */
const XP_DONE = XP_ISLAND;

interface Attempt {
  n: number;
  date: string;
  scores: DriverScores;
  xp: number;
}

interface State {
  seed: string;
  answers: Answer[];
  current: number;
  xp: number;
  earnsXp: boolean;
  islandsPaid: number[];
  remind: boolean;
  history: Attempt[];
}

const empty = (): Answer[] => PAIRS.map(() => null);

function scenario(): string {
  return sceneOr('ca', 'new');
}

/** The Figma S1 example and the two attempts before it (compare view). */
const HISTORY: Attempt[] = [
  {
    n: 1,
    date: '2026-04-12',
    xp: 100,
    scores: { A: 10, B: 10, C: 12, D: 9, E: 14, F: 12, G: 14, H: 13, I: 14 },
  },
  {
    n: 2,
    date: '2026-07-20',
    xp: 0,
    scores: { A: 12, B: 9, C: 10, D: 9, E: 16, F: 9, G: 16, H: 13, I: 14 },
  },
  {
    n: 3,
    date: '2026-10-01',
    xp: 0,
    scores: { A: 11, B: 9, C: 10, D: 8, E: 18, F: 9, G: 17, H: 12, I: 14 },
  },
];

/** Answers that lean toward the Figma example for seeded scenarios. */
function seeded(count: number, skip: number[] = []): Answer[] {
  return PAIRS.map((p, i) => {
    if (skip.includes(i)) return 'skip';
    if (i >= count) return null;
    const lean = ['E', 'G', 'I'];
    return lean.includes(p.this.driver)
      ? 0
      : lean.includes(p.that.driver)
        ? 3
        : (((i % 2) + 1) as 1 | 2);
  });
}

function fresh(seed: string): State {
  const base: State = {
    seed,
    answers: empty(),
    current: 0,
    xp: 0,
    earnsXp: true,
    islandsPaid: [],
    remind: true,
    history: [],
  };
  switch (seed) {
    case 'resume':
      return { ...base, answers: seeded(13), current: 13, xp: 25, islandsPaid: [1] };
    case 'final':
      return {
        ...base,
        answers: seeded(36, [11, 26]),
        current: 35,
        xp: 75,
        islandsPaid: [1, 2, 3],
      };
    case 'done':
      return { ...base, history: HISTORY, earnsXp: false };
    default:
      return base;
  }
}

function load(): State {
  const sc = scenario();
  try {
    const raw = sessionStorage.getItem(KEY);
    const s = raw ? (JSON.parse(raw) as State) : null;
    if (s && (!sc || s.seed === sc)) return s;
  } catch {
    /* private mode */
  }
  const s = fresh(sc);
  save(s);
  return s;
}

function save(s: State): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    /* private mode */
  }
}

function status(s: State): AssessmentState['status'] {
  if (s.answers.some((a) => a !== null)) return 'in-progress';
  return s.history.length ? 'done' : 'new';
}

function view(s: State): AssessmentState {
  return {
    status: status(s),
    pairs: PAIRS,
    islands: ISLANDS,
    answers: s.answers,
    current: s.current,
    xp: s.xp,
    earnsXp: s.earnsXp,
    remind: s.remind,
    attempts: s.history.length,
  };
}

function result(s: State, n?: number): AssessmentResult | null {
  const a = s.history.find((h) => h.n === n) ?? s.history.at(-1);
  if (!a) return null;
  const prev = s.history.find((h) => h.n === a.n - 1) ?? null;
  return {
    attempt: a.n,
    date: a.date,
    scores: a.scores,
    ranking: rankDrivers(a.scores),
    archetype: matchArchetype(a.scores, ARCHETYPES),
    drivers: DRIVERS,
    previous: prev?.scores ?? null,
    attempts: s.history
      .map((h) => ({ n: h.n, date: h.date, archetype: matchArchetype(h.scores, ARCHETYPES).name }))
      .reverse(),
    xpEarned: a.xp,
    mandatoryCourses: 2,
  };
}

const offline = () => typeof navigator !== 'undefined' && !navigator.onLine;

/** Read by the Launchpad mock so its assessment card follows real progress. */
export function assessmentSnapshot(): {
  answered: number;
  done: boolean;
  archetype: { name: string; emoji: string; traits: string[]; summary: string } | null;
} | null {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as State;
    const last = s.history.at(-1);
    const answered = s.answers.filter(isAnswered).length;
    if (!last) return { answered, done: false, archetype: null };
    const arch = matchArchetype(last.scores, ARCHETYPES);
    return {
      answered,
      done: true,
      archetype: {
        name: arch.name,
        emoji: arch.emoji,
        summary: arch.summary,
        traits: rankDrivers(last.scores)
          .slice(0, 3)
          .map((d) => DRIVERS.find((x) => x.id === d)?.name ?? d),
      },
    };
  } catch {
    return null;
  }
}

export const assessmentHandlers = [
  http.get('/api/assessments/career', async () => {
    await delay(400);
    if (scenario() === 'error') return new HttpResponse(null, { status: 503 });
    return HttpResponse.json(view(load()));
  }),

  http.put('/api/assessments/career/answers', async ({ request }) => {
    await delay(150);
    if (offline()) return HttpResponse.error();
    const body = (await request.json()) as { answers: Answer[]; current: number };
    const s = load();
    s.answers = body.answers;
    s.current = body.current;
    let earned = 0;
    for (const i of ISLANDS) {
      if (i.n === ISLANDS.length || s.islandsPaid.includes(i.n)) continue;
      const done = s.answers.slice(i.from - 1, i.to).every(isAnswered);
      if (done) {
        s.islandsPaid.push(i.n);
        if (s.earnsXp) earned += XP_ISLAND;
      }
    }
    s.xp += earned;
    creditXp(earned);
    save(s);
    return HttpResponse.json({ xp: s.xp, xpEarned: earned });
  }),

  http.post('/api/assessments/career/submit', async ({ request }) => {
    await delay(900);
    if (offline()) return HttpResponse.error();
    const { answers } = (await request.json()) as { answers: Answer[] };
    if (answers.length !== PAIRS.length || !answers.every(isAnswered))
      return HttpResponse.json({ code: 'incomplete' }, { status: 422 });
    const s = load();
    const xp = s.earnsXp ? XP_DONE : 0;
    creditXp(xp);
    s.history.push({
      n: s.history.length + 1,
      date: new Date().toISOString().slice(0, 10),
      scores: scoreDrivers(PAIRS, answers),
      xp: s.xp + xp,
    });
    s.answers = empty();
    s.current = 0;
    s.xp = 0;
    s.islandsPaid = [];
    s.earnsXp = false;
    save(s);
    return HttpResponse.json(result(s));
  }),

  http.get('/api/assessments/career/result', async ({ request }) => {
    await delay(350);
    const n = Number(new URL(request.url).searchParams.get('attempt')) || undefined;
    const r = result(load(), n);
    return r ? HttpResponse.json(r) : new HttpResponse(null, { status: 404 });
  }),

  http.post('/api/assessments/career/restart', async () => {
    await delay(200);
    const s = load();
    s.answers = empty();
    s.current = 0;
    save(s);
    return HttpResponse.json(view(s));
  }),

  http.post('/api/assessments/career/retake', async () => {
    await delay(250);
    const s = load();
    s.answers = empty();
    s.current = 0;
    s.xp = 0;
    s.islandsPaid = [];
    s.earnsXp = false;
    save(s);
    return HttpResponse.json(view(s));
  }),

  http.put('/api/assessments/career/remind', async ({ request }) => {
    await delay(150);
    const { remind } = (await request.json()) as { remind: boolean };
    const s = load();
    s.remind = remind;
    save(s);
    return HttpResponse.json({ remind });
  }),
];
```

## `src/mocks/auth.handlers.ts`

```ts
import { mockPhoto } from './profile.handlers';
import { HttpResponse, delay, http } from 'msw';
import {
  isNewLearner,
  knownLogins,
  learnerName,
  persona,
  rememberLogin,
  rememberName,
  setPersona,
} from './persona';
import { mockProgress } from './quest.handlers';

/**
 * Mock auth. Codes: 123456 is right; anything else is wrong (5 tries); "000000" = expired.
 * Phones ending 0000 are locked. Password "letmein123" works for any email; "locked@…" is locked.
 *
 * A-308: log in only works for a registered mobile or email. Registered = the sample learner
 * (+91 98765 43210, yash.p@gmail.com), yash.verma@gmail.com, locked accounts, and any email that
 * signed up or used Google in this tab. Anything else → 404 account-not-found → sign-up.
 */
const tries = new Map<string, number>();
/** Corrections #7: unlimited tries for people; 20 wrong codes in a row is the hidden anti-bot limit. */
const MAX_TRIES = 20;

const user = { id: 'u-1', firstName: 'Yash', email: 'yash.p@gmail.com', phone: '+91 98765 43210' };

/** The mock backend runs inside the page (A-157), so it sets the session cookie itself. */
export function mockSession(on: boolean): void {
  document.cookie = on ? 'sm_mock_session=1; Path=/' : 'sm_mock_session=; Max-Age=0; Path=/';
}
const hasSession = () => document.cookie.split('; ').includes('sm_mock_session=1');
/** Logging in with mobile or email = a returning learner (A-232). */
const signedIn = (body: object) => {
  mockSession(true);
  // A new learner who saved & exited mid-registration is the same learner logging back in (A-252).
  if (!(persona() === 'new' && mockProgress.completed < 8)) setPersona('existing');
  return HttpResponse.json(body);
};

function fail(status: number, code: string, message: string) {
  return HttpResponse.json({ message, errors: { code: [code] } }, { status });
}

function formatPhone(phone: string): string {
  const d = phone.replace(/\D/g, '');
  const local = d.slice(-10);
  return `+91 ${local.slice(0, 5)} ${local.slice(5)}`;
}

const emailTries = new Map<string, number>();
const SAMPLE_PHONE = '9876543210';
const SAMPLE_EMAILS = ['yash.p@gmail.com', 'yash.verma@gmail.com'];
const isRegisteredEmail = (email: string) => {
  const e = email.trim().toLowerCase();
  return e.startsWith('locked@') || SAMPLE_EMAILS.includes(e) || knownLogins().includes(e);
};
const isRegisteredPhone = (phone: string) => {
  const local = phone.replace(/\D/g, '').slice(-10);
  return local === SAMPLE_PHONE || local.endsWith('0000') || knownLogins().includes(local);
};
const notFound = () => fail(404, 'account-not-found', 'No account for that login');
const pending = new Map<string, { firstName: string; lastName: string }>();

function emailChallenge(email: string) {
  emailTries.set(email, MAX_TRIES);
  return { email, resendIn: 42, validFor: 10, triesLeft: MAX_TRIES };
}

export const authHandlers = [
  http.post('/api/auth/register', async ({ request }) => {
    await delay(600);
    const body = (await request.json()) as { email: string; firstName: string; lastName?: string };
    if (isRegisteredEmail(body.email)) {
      return HttpResponse.json(
        {
          message: 'Email taken',
          errors: { code: ['email-taken'], email: ['Already in the squad.'] },
        },
        { status: 409 },
      );
    }
    pending.set(body.email, { firstName: body.firstName, lastName: body.lastName ?? '' });
    return HttpResponse.json(emailChallenge(body.email));
  }),

  http.post('/api/auth/email/resend', async ({ request }) => {
    await delay(400);
    const { email } = (await request.json()) as { email: string };
    return HttpResponse.json(emailChallenge(email));
  }),

  http.post('/api/auth/email/verify', async ({ request }) => {
    await delay(500);
    const { email, code } = (await request.json()) as { email: string; code: string };
    if (code === '000000') return fail(410, 'code-expired', 'Code expired');
    if (code === '123456') {
      // A-232: a brand-new account → every feature starts in its first-time state.
      setPersona('new');
      mockSession(true);
      rememberLogin(email);
      rememberName(
        pending.get(email)?.firstName ?? 'there',
        pending.get(email)?.lastName ?? '',
        email,
      );
      return HttpResponse.json({
        id: 'u-new',
        firstName: pending.get(email)?.firstName ?? 'there',
        email,
      });
    }
    const left = (emailTries.get(email) ?? MAX_TRIES) - 1;
    emailTries.set(email, left);
    if (left <= 0) return fail(429, 'too-many-tries', 'Too many tries');
    return HttpResponse.json(
      { message: 'Wrong code', errors: { code: ['invalid-code'], triesLeft: [String(left)] } },
      { status: 422 },
    );
  }),

  http.post('/api/auth/otp', async ({ request }) => {
    await delay(500);
    const { phone } = (await request.json()) as { phone: string };
    if (!isRegisteredPhone(phone)) return notFound();
    if (phone.endsWith('0000')) return fail(423, 'account-locked', 'Account locked');
    tries.set(phone, MAX_TRIES);
    return HttpResponse.json({ sentTo: formatPhone(phone), resendIn: 30, triesLeft: MAX_TRIES });
  }),

  http.post('/api/auth/otp/verify', async ({ request }) => {
    await delay(500);
    const { phone, code } = (await request.json()) as { phone: string; code: string };
    if (code === '000000') return fail(410, 'code-expired', 'Code expired');
    if (code === '123456') return signedIn({ ...user, phone: formatPhone(phone) });
    const left = (tries.get(phone) ?? MAX_TRIES) - 1;
    tries.set(phone, left);
    if (left <= 0) return fail(429, 'too-many-tries', 'Too many tries');
    return HttpResponse.json(
      { message: 'Wrong code', errors: { code: ['invalid-code'], triesLeft: [String(left)] } },
      { status: 422 },
    );
  }),

  http.post('/api/auth/email/login-code', async ({ request }) => {
    await delay(500);
    const { email } = (await request.json()) as { email: string };
    if (!isRegisteredEmail(email)) return notFound();
    if (email.startsWith('locked@')) return fail(423, 'account-locked', 'Account locked');
    tries.set(email, MAX_TRIES);
    const [name = '', domain = ''] = email.split('@');
    return HttpResponse.json({
      sentTo: `${name.slice(0, 2)}•••@${domain}`,
      resendIn: 30,
      triesLeft: MAX_TRIES,
    });
  }),

  http.post('/api/auth/email/login-verify', async ({ request }) => {
    await delay(500);
    const { email, code } = (await request.json()) as { email: string; code: string };
    if (code === '000000') return fail(410, 'code-expired', 'Code expired');
    if (code === '123456') return signedIn({ ...user, email });
    const left = (tries.get(email) ?? MAX_TRIES) - 1;
    tries.set(email, left);
    if (left <= 0) return fail(429, 'too-many-tries', 'Too many tries');
    return HttpResponse.json(
      { message: 'Wrong code', errors: { code: ['invalid-code'], triesLeft: [String(left)] } },
      { status: 422 },
    );
  }),

  /** A-253: `mock:existing` → the sample learner; `mock:new:First:Last` → a brand-new account. */
  http.post('/api/auth/google', async ({ request }) => {
    await delay(500);
    const { credential } = (await request.json()) as { credential: string };
    if (credential.startsWith('mock:new:')) {
      const [, , first = 'there', last = ''] = credential.split(':');
      const email = `${first}.${last}@gmail.com`.toLowerCase();
      setPersona('new');
      rememberName(first, last, email);
      rememberLogin(email);
      mockSession(true);
      return HttpResponse.json({
        user: { id: 'u-google-new', firstName: first, email },
        isNew: true,
      });
    }
    if (credential === 'mock:existing') {
      mockSession(true);
      setPersona('existing');
      return HttpResponse.json({ user: { ...user, email: 'yash.p@gmail.com' }, isNew: false });
    }
    return fail(401, 'google-invalid', 'Google token rejected');
  }),

  http.post('/api/auth/login', async ({ request }) => {
    await delay(500);
    const { email, password } = (await request.json()) as { email: string; password: string };
    if (email.startsWith('locked@')) return fail(423, 'account-locked', 'Account locked');
    if (password !== 'letmein123') return fail(401, 'wrong-credentials', 'Wrong email or password');
    return signedIn({ ...user, email });
  }),

  http.post('/api/auth/password-reset', async () => {
    await delay(400);
    return HttpResponse.json({ resendIn: 45, validFor: 30 });
  }),

  http.post('/api/auth/logout', () => {
    mockSession(false);
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/session', () =>
    hasSession()
      ? HttpResponse.json(
          {
            ...(isNewLearner() ? { ...user, id: 'u-new', firstName: learnerName().first } : user),
            photoUrl: mockPhoto(),
          },
        )
      : HttpResponse.json({ message: 'No session' }, { status: 401 }),
  ),
];
```

## `src/mocks/badges.data.ts`

```ts
import { QUESTS } from '../app/core/quests/quest-catalog';
import { isNewLearner } from './persona';
import { mockProgress } from './quest.handlers';

/**
 * Mock badge catalogue (Figma My Badges 170:4660; note 177:7597 "to confirm": final catalogue and
 * categories). 48 badges in 6 categories; 2 earned. The showcase (up to 3 pinned) lives in
 * sessionStorage and is shared by My Stats and the Profile showcase.
 */

type Category =
  'quests' | 'streaks' | 'learning' | 'quizzes' | 'assessments' | 'social' | 'special';
type Rarity = 'common' | 'rare' | 'epic' | 'legendary';
type State = 'earned' | 'progress' | 'locked' | 'secret';

export interface MockBadge {
  id: string;
  name: string;
  icon: string;
  category: Category;
  rarity: Rarity;
  state: State;
  earnedOn: string | null;
  progressLabel: string | null;
  percent: number;
  howTo: string;
  ownedPct: number;
  cta: { label: string; url: string } | null;
  tiers: { reached: number; total: number } | null;
  /** Quest badges show the quest's 3D illustration (corrections #24). */
  image?: string | null;
}

const QUIZ = '/learn/brain-vita/non-verbal-reasoning/quiz';
const BV = '/learn/brain-vita';

function b(
  id: string,
  name: string,
  icon: string,
  category: Category,
  rarity: Rarity,
  state: State,
  howTo: string,
  ownedPct: number,
  extra: Partial<MockBadge> = {},
): MockBadge {
  return {
    id,
    name,
    icon,
    category,
    rarity,
    state,
    earnedOn: null,
    progressLabel: null,
    percent: 0,
    howTo,
    ownedPct,
    cta: null,
    tiers: null,
    ...extra,
  };
}

const secret = (id: string, category: Category) =>
  b(id, 'Secret', 'star', category, 'epic', 'secret', 'Keep learning. This one is a surprise.', 6);

export const BADGES: MockBadge[] = [
  // Learning (14)
  b(
    'first-steps',
    'First Steps',
    'star',
    'learning',
    'legendary',
    'earned',
    'Finish your first brief.',
    3,
    {
      earnedOn: '2026-09-12',
      percent: 100,
    },
  ),
  b(
    'brain-vita-boss',
    'Brain-Vita Boss',
    'trophy',
    'learning',
    'epic',
    'progress',
    'Finish every Brain-Vita module: verbal, non-verbal, arithmetic and data.',
    14,
    {
      progressLabel: '98.86%',
      percent: 98.86,
      cta: { label: 'Finish Brain-Vita', url: BV },
    },
  ),
  b(
    'knook-explorer',
    'Knook Explorer',
    'target',
    'learning',
    'rare',
    'progress',
    'Open a module in 5 different Knooks.',
    31,
    {
      progressLabel: '2 / 5 Knooks',
      percent: 40,
      cta: { label: 'Explore Knooks', url: '/home' },
    },
  ),
  b(
    'cpd-champ',
    'CPD Champ',
    'shield-check',
    'learning',
    'rare',
    'locked',
    'Earn 10 CPD hours from finished courses.',
    22,
    { progressLabel: '0 / 10 hrs' },
  ),
  b(
    'hospitality-hero',
    'Hospitality Hero',
    'briefcase',
    'learning',
    'rare',
    'locked',
    'Finish the Hospitality course.',
    26,
    {
      progressLabel: '0%',
    },
  ),
  b(
    'it-gyan-guru',
    'IT Gyan Guru',
    'lightbulb',
    'learning',
    'rare',
    'locked',
    'Finish the IT Gyan course.',
    24,
    {
      progressLabel: '0%',
    },
  ),
  b(
    'banking-pro',
    'Banking Pro',
    'coins',
    'learning',
    'epic',
    'locked',
    'Finish the Banking course.',
    12,
    {
      progressLabel: '0%',
    },
  ),
  b(
    'globe-trotter',
    'Globe Trotter',
    'compass',
    'learning',
    'epic',
    'locked',
    'Finish International Opportunities.',
    9,
    {
      progressLabel: '0%',
    },
  ),
  b('auto-ace', 'Auto Ace', 'zap', 'learning', 'epic', 'locked', 'Finish Auto Components.', 8, {
    progressLabel: '0%',
  }),
  b(
    'module-master',
    'Module Master',
    'layout-grid',
    'learning',
    'rare',
    'locked',
    'Finish 10 modules.',
    20,
    { progressLabel: '0 / 10' },
  ),
  b(
    'course-closer',
    'Course Closer',
    'check-circle',
    'learning',
    'common',
    'locked',
    'Finish your first full course.',
    54,
    { progressLabel: '0 / 1' },
  ),
  b('bookworm', 'Bookworm', 'bookmark', 'learning', 'common', 'locked', 'Bookmark 10 briefs.', 58, {
    progressLabel: '0 / 10',
  }),
  b('deep-diver', 'Deep Diver', 'play', 'learning', 'rare', 'locked', 'Read 50 briefs.', 27, {
    progressLabel: '0 / 50',
  }),
  secret('secret-learning', 'learning'),
  // Quizzes (10)
  b(
    'quiz-starter',
    'Quiz Starter',
    'check-circle',
    'quizzes',
    'epic',
    'earned',
    'Pass your first quiz.',
    18,
    {
      earnedOn: '2026-09-20',
      percent: 100,
    },
  ),
  b(
    'ten-quizzes',
    'Quiz Regular',
    'lightbulb',
    'quizzes',
    'common',
    'progress',
    'Pass 10 quizzes.',
    61,
    {
      progressLabel: '4 / 10',
      percent: 40,
      cta: { label: 'Take a quiz', url: QUIZ },
    },
  ),
  b(
    'perfect-score',
    'Perfect Score',
    'star',
    'quizzes',
    'rare',
    'locked',
    'Get every answer right on a quiz.',
    33,
    {
      progressLabel: '0 / 1',
      cta: { label: 'Take a quiz', url: QUIZ },
    },
  ),
  b(
    'sharp-shooter',
    'Sharp Shooter',
    'target',
    'quizzes',
    'rare',
    'locked',
    'Score 80%+ on 3 quizzes.',
    29,
    {
      progressLabel: '0 / 3',
    },
  ),
  b(
    'quiz-streak',
    'On a Roll',
    'flame',
    'quizzes',
    'epic',
    'locked',
    'Pass a quiz 5 days in a row.',
    11,
    {
      progressLabel: '0 / 5 days',
    },
  ),
  b(
    'quick-thinker',
    'Quick Thinker',
    'clock',
    'quizzes',
    'rare',
    'locked',
    'Finish a quiz in under a minute.',
    21,
  ),
  b(
    'retry-hero',
    'Retry Hero',
    'repeat',
    'quizzes',
    'common',
    'locked',
    'Get a missed question right in practice.',
    52,
  ),
  b(
    'fifty-quizzes',
    'Quiz Legend',
    'trophy',
    'quizzes',
    'legendary',
    'progress',
    'Pass 50 quizzes.',
    2,
    {
      progressLabel: '4 / 50',
      percent: 8,
    },
  ),
  b(
    'night-quiz',
    'Midnight Quizzer',
    'sparkle',
    'quizzes',
    'rare',
    'locked',
    'Pass a quiz after 10 pm.',
    23,
  ),
  secret('secret-quiz', 'quizzes'),
  // Streaks (6)
  b(
    'streak-starter',
    'Streak Starter',
    'flame',
    'streaks',
    'rare',
    'progress',
    'Learn 3 days in a row.',
    38,
    {
      progressLabel: '2 / 3 days',
      percent: 66.67,
      cta: { label: 'Finish a quiz today', url: QUIZ },
    },
  ),
  b(
    'streak-master',
    'Streak Master',
    'flame',
    'streaks',
    'legendary',
    'locked',
    'Keep a 3, 7 and 30-day streak to fill all three pips.',
    4,
    { progressLabel: 'Tier 0 of 3', tiers: { reached: 0, total: 3 } },
  ),
  b(
    'night-owl',
    'Night Owl',
    'flame',
    'streaks',
    'rare',
    'locked',
    'Learn after 10 pm on 5 nights.',
    24,
    {
      progressLabel: '0 / 5 nights',
    },
  ),
  b(
    'comeback-kid',
    'Comeback Kid',
    'flame',
    'streaks',
    'common',
    'locked',
    'Return after 7 days away.',
    51,
    {
      progressLabel: 'Return after 7 days',
    },
  ),
  b(
    'early-bird',
    'Early Bird',
    'clock',
    'streaks',
    'rare',
    'locked',
    'Learn before 8 am on 5 mornings.',
    19,
    {
      progressLabel: '0 / 5 mornings',
    },
  ),
  secret('secret-streak', 'streaks'),
  // Assessments (8)
  b(
    'card-collector',
    'Card Collector',
    'layout-grid',
    'assessments',
    'epic',
    'progress',
    'Unlock 25 skill cards.',
    7,
    {
      progressLabel: '11 / 25 cards',
      percent: 44,
    },
  ),
  b(
    'expert-eye',
    'Expert Eye',
    'crown',
    'assessments',
    'legendary',
    'progress',
    'Reach Expert on 5 skill cards.',
    3,
    {
      progressLabel: '2 / 5',
      percent: 40,
    },
  ),
  b(
    'tier-up',
    'Tier Up',
    'trend-up',
    'assessments',
    'rare',
    'locked',
    'Move a skill card up a tier.',
    28,
  ),
  b(
    'all-rounder',
    'All-Rounder',
    'target',
    'assessments',
    'epic',
    'locked',
    'Reach Advanced in 4 different skills.',
    9,
  ),
  b(
    'career-clarity',
    'Career Clarity',
    'compass',
    'assessments',
    'common',
    'locked',
    'Finish Career Drivers.',
    56,
  ),
  b(
    'digital-native',
    'Digital Native',
    'zap',
    'assessments',
    'rare',
    'locked',
    'Score 90%+ in Digital Skills.',
    21,
  ),
  b(
    'leader-board',
    'Born Leader',
    'users',
    'assessments',
    'rare',
    'locked',
    'Score 90%+ in Leadership.',
    22,
  ),
  secret('secret-assess', 'assessments'),
  // Social (6)
  b(
    'cheerleader',
    'Cheerleader',
    'thumbs-up',
    'social',
    'common',
    'locked',
    'Cheer 10 players in the Arena.',
    60,
    {
      progressLabel: '0 / 10',
      cta: { label: 'Open the Arena', url: '/arena' },
    },
  ),
  b(
    'challenger',
    'Challenger',
    'trophy',
    'social',
    'rare',
    'locked',
    'Win 3 Arena challenges.',
    25,
  ),
  b(
    'top-ten',
    'Top 10',
    'crown',
    'social',
    'epic',
    'locked',
    'Finish a week in your batch’s top 10.',
    15,
    {
      cta: { label: 'Open the Arena', url: '/arena' },
    },
  ),
  b('squad-up', 'Squad Up', 'users', 'social', 'common', 'locked', 'Add 3 friends.', 57),
  b(
    'helping-hand',
    'Helping Hand',
    'message-circle',
    'social',
    'rare',
    'locked',
    'Share a brief with a friend 5 times.',
    26,
  ),
  secret('secret-social', 'social'),
  // Special & seasonal (4)
  b('xp-1k', 'XP 1K Club', 'zap', 'special', 'rare', 'progress', 'Earn 1,000 XP in total.', 21, {
    progressLabel: '529 / 1,000',
    percent: 52.9,
  }),
  b(
    'founding-learner',
    'Founding Learner',
    'sparkle',
    'special',
    'legendary',
    'locked',
    'Join in Skillmuni’s first year.',
    1,
  ),
  b(
    'festive-2026',
    'Festive Feat',
    'gift',
    'special',
    'epic',
    'locked',
    'Learn on 5 days during the festive week.',
    10,
  ),
  secret('secret-special', 'special'),
];

const KEY = 'sm_mock_showcase';
const DEFAULT_SHOWCASE = ['first-steps', 'quiz-starter'];
/** A new learner's showcase starts with their latest quest badges, so earned badges show up at once. */
const defaultShowcase = () =>
  isNewLearner()
    ? questBadges()
        .filter((x) => x.state === 'earned')
        .slice(-3)
        .reverse()
        .map((x) => x.id)
    : DEFAULT_SHOWCASE;

export function showcase(): string[] {
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as string[]) : defaultShowcase();
  } catch {
    return defaultShowcase();
  }
}

export function setShowcase(ids: string[]): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(ids));
  } catch {
    /* private mode */
  }
}

const QUEST_ICONS = [
  'user',
  'phone',
  'lightbulb',
  'star',
  'briefcase',
  'compass',
  'paperclip',
  'shield-check',
];
const QUEST_RARITY: Rarity[] = [
  'common',
  'common',
  'common',
  'rare',
  'rare',
  'rare',
  'epic',
  'legendary',
];

/** One badge per profile quest (A-292): earned the moment the quest is done, so the library matches it. */
export function questBadges(): MockBadge[] {
  const done = mockProgress.completed;
  const today = new Date().toISOString().slice(0, 10);
  return QUESTS.map((q, i) => {
    const earned = i < done;
    const current = i === done;
    const percent = earned
      ? 100
      : current
        ? Math.round((mockProgress.partsDone / q.parts) * 100)
        : 0;
    return b(
      `quest-${q.slug}`,
      q.badge,
      QUEST_ICONS[i] ?? 'star',
      'quests',
      QUEST_RARITY[i] ?? 'common',
      earned ? 'earned' : current ? 'progress' : 'locked',
      `Finish the ${q.name} quest.`,
      Math.max(8, 72 - i * 8),
      {
        earnedOn: earned ? today : null,
        percent,
        progressLabel: current ? `${mockProgress.partsDone} / ${q.parts} parts` : null,
        image: `assets/quests/quest-${q.id}.png`,
        cta: earned
          ? null
          : { label: current ? 'Continue quest' : 'Go to quests', url: `/quests/${q.slug}` },
      },
    );
  });
}

/** The catalogue as this learner sees it: a new learner has earned nothing yet (A-232). */
export function badgeList(): MockBadge[] {
  if (!isNewLearner()) return [...questBadges(), ...BADGES];
  return [
    ...questBadges(),
    ...BADGES.map((x): MockBadge =>
      x.state === 'earned' || x.state === 'progress'
        ? {
            ...x,
            state: 'progress',
            earnedOn: null,
            percent: 0,
            progressLabel: x.progressLabel ? '0%' : null,
            tiers: x.tiers ? { ...x.tiers, reached: 0 } : null,
          }
        : x,
    ),
  ];
}

export function earnedBadges(): MockBadge[] {
  return badgeList().filter((x) => x.state === 'earned');
}

/** Closest to unlock: highest progress among badges not yet earned. */
export function upNext(n: number): MockBadge[] {
  return badgeList()
    .filter((x) => x.state === 'progress')
    .sort((a, c) => c.percent - a.percent)
    .slice(0, n);
}
```

## `src/mocks/claim.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { environment } from '../environments/environment';
import { mockSession } from './auth.handlers';
import { markClaimed } from './matches.handlers';
import { mockProgress, persistQuests, seedAnswers, syncLevel } from './quest.handlers';
import { QUESTS, questXp } from '../app/core/quests/quest-catalog';

/** A claimed account arrives with quests 1–5 done (A-321: their real XP, 550); 6–8 are left. */
const CLAIMED_XP = QUESTS.slice(0, 5).reduce((n, q) => n + questXp(q), 0);
const xpOf = (id: number): number => {
  const q = QUESTS[id - 1];
  return q ? questXp(q) : 0;
};

/**
 * Institute claim mocks. Links: /claim/psa-15-aarav works; /claim/claimed and /claim/expired are used up;
 * anything else is not found. Code 123456 is right, 000000 is expired, others are wrong (5 tries).
 */
/** Corrections #7: unlimited tries for people; 20 wrong codes in a row is the hidden anti-bot limit. */
const MAX_TRIES = 20;
let tries = MAX_TRIES;
const rejected = new Set<string>();

/** Claim progress survives a full reload (learner reopens the SMS link after editing a quest). */
const KEY = 'sm_mock_claim';
interface Progress {
  verified: boolean;
  consented: boolean;
  confirmed: string[];
  result: object | null;
}
function load(): Progress {
  try {
    return {
      verified: false,
      consented: false,
      confirmed: [],
      result: null,
      ...JSON.parse(sessionStorage.getItem(KEY) ?? '{}'),
    };
  } catch {
    return { verified: false, consented: false, confirmed: [], result: null };
  }
}
const state = load();
const confirmed = new Set<string>(state.confirmed);
function save(): void {
  state.confirmed = [...confirmed];
  try {
    sessionStorage.setItem(KEY, JSON.stringify(state));
  } catch {
    /* private mode: this page only */
  }
}
const market = () => (environment.market === 'ZA' ? 'ZA' : 'IN');

const INVITE = {
  IN: {
    institute: 'Pune Skills Academy',
    name: 'Aarav Mehta',
    programme: 'Customer Care Executive (Domestic Voice) · NSQF L4',
    batch: 'Pune batch 15 · completed 30 Sep 2026',
    fromCv: 'Contact, education, 8 skills, 2 languages, 1 job',
    sentTo: '+91 98•••• 4521',
    certificate: 'NSQF L4 certificate',
  },
  ZA: {
    institute: 'Ubuntu Skills Academy',
    name: 'Nkosazana Dlamini',
    programme: 'Contact Centre Support · NQF 4 learnership',
    batch: 'Randburg cohort 7 · completed 30 Sep 2026',
    fromCv: 'Contact, education, 8 skills, 2 languages, 1 job',
    sentTo: '+27 82••• 4521',
    certificate: 'NQF 4 certificate',
  },
};

const row = (label: string, value: string, source: string | null, issue?: string) => ({
  label,
  value,
  source,
  ...(issue ? { issue } : {}),
});

const SECTIONS = {
  IN: [
    {
      slug: 'who-dis',
      quest: 1,
      title: 'Who Dis?',
      summary: 'Aarav Mehta · date of birth added by you',
      rows: [row('Name', 'Aarav Mehta', 'institute'), row('Date of birth', '14 Mar 2005', 'you')],
    },
    {
      slug: 'hit-me-up',
      quest: 2,
      title: 'Hit Me Up',
      summary: '+91 98•••• 4521 verified · Kothrud, Pune 411038',
      rows: [
        row('Mobile', '+91 98•••• 4521', 'verified'),
        row('Address', 'Kothrud, Pune 411038', 'institute'),
      ],
    },
    {
      slug: 'big-brain-stuff',
      quest: 3,
      title: 'Big Brain Stuff',
      summary: '12th (HSC) · NSQF L4 certificate',
      rows: [
        row('Highest level', '12th (HSC) · Maharashtra State Board · 2023', 'institute'),
        row('Certificate', 'Customer Care Executive (Domestic Voice) · NSQF L4 · 2026', 'verified'),
        row('Marks', '72% · optional', null),
      ],
    },
    {
      slug: 'your-flex-era',
      quest: 4,
      title: 'Your Flex Era',
      summary: '8 skills · Hindi (Native), English (Fluent), Marathi (Conversational)',
      rows: [
        row('Skills', 'Customer service, Active listening, MS Excel, Data entry +4', 'institute'),
        row('Languages', 'Hindi (Native), English (Fluent), Marathi (Conversational)', 'institute'),
      ],
    },
    {
      slug: 'work-lore',
      quest: 5,
      title: 'Work Lore',
      summary: 'Customer support intern · Concentrix · end date missing',
      rows: [
        row(
          'Customer support intern',
          'Concentrix · Jun 2025 – ?',
          'institute',
          'End date missing. Still there? Add it or tap Looks right.',
        ),
      ],
    },
  ],
  ZA: [
    {
      slug: 'who-dis',
      quest: 1,
      title: 'Who Dis?',
      summary: 'Nkosazana Dlamini · date of birth added by you',
      rows: [
        row('Name', 'Nkosazana Dlamini', 'institute'),
        row('Date of birth', '2 Aug 2004', 'you'),
      ],
    },
    {
      slug: 'hit-me-up',
      quest: 2,
      title: 'Hit Me Up',
      summary: '+27 82••• 4521 verified · Randburg 2194',
      rows: [
        row('Mobile', '+27 82••• 4521', 'verified'),
        row('Address', 'Randburg, Johannesburg 2194', 'institute'),
      ],
    },
    {
      slug: 'big-brain-stuff',
      quest: 3,
      title: 'Big Brain Stuff',
      summary: 'Matric (NSC) · NQF 4 certificate',
      rows: [
        row('Highest level', 'Matric (NSC) · 2022', 'institute'),
        row('Certificate', 'Contact Centre Support · NQF 4 · 2026', 'verified'),
        row('Marks', '68% · optional', null),
      ],
    },
    {
      slug: 'your-flex-era',
      quest: 4,
      title: 'Your Flex Era',
      summary: '8 skills · isiZulu (Native), English (Fluent), Sesotho (Conversational)',
      rows: [
        row('Skills', 'Customer service, Active listening, Data capturing +5', 'institute'),
        row(
          'Languages',
          'isiZulu (Native), English (Fluent), Sesotho (Conversational)',
          'institute',
        ),
      ],
    },
    {
      slug: 'work-lore',
      quest: 5,
      title: 'Work Lore',
      summary: 'Call centre intern · Ubuntu CX · end date missing',
      rows: [
        row(
          'Call centre intern',
          'Ubuntu CX · Jun 2025 – ?',
          'institute',
          'End date missing. Still there? Add it or tap Looks right.',
        ),
      ],
    },
  ],
};

function sections() {
  return SECTIONS[market()].map((sec) => ({
    ...sec,
    status: confirmed.has(sec.slug) ? 'confirmed' : sec.slug === 'work-lore' ? 'fix' : 'review',
  }));
}

// Re-apply a claim made earlier in this tab after a full reload.
if (state.consented) mockProgress.completed = Math.max(mockProgress.completed, 5);
if (state.result) {
  mockProgress.xp = Math.max(mockProgress.xp, CLAIMED_XP);
  mockProgress.badges = Math.max(mockProgress.badges, 5);
  syncLevel();
  markClaimed();
}

const gone = (code: string) =>
  HttpResponse.json({ message: code, errors: { code: [code] } }, { status: 410 });

export const claimHandlers = [
  http.get('/api/claims/:token', async ({ params }) => {
    await delay(600);
    const token = String(params['token']);
    if (token === 'claimed' || token === 'expired') return gone(token);
    if (rejected.has(token)) return gone('deleted');
    if (token !== 'psa-15-aarav') return new HttpResponse(null, { status: 404 });
    tries = MAX_TRIES;
    const resume = state.verified
      ? {
          stage: state.result ? 'confirmed' : state.consented ? 'validate' : 'consent',
          sections: state.consented ? sections() : [],
          result: state.result,
        }
      : undefined;
    return HttpResponse.json({
      ...INVITE[environment.market === 'ZA' ? 'ZA' : 'IN'],
      resendIn: 24,
      triesLeft: MAX_TRIES,
      deleteBy: '2026-10-30',
      ...(resume ? { resume } : {}),
    });
  }),

  http.post('/api/claims/:token/otp', async () => {
    await delay(400);
    tries = MAX_TRIES;
    const invite = INVITE[environment.market === 'ZA' ? 'ZA' : 'IN'];
    return HttpResponse.json({ sentTo: invite.sentTo, resendIn: 30, triesLeft: MAX_TRIES });
  }),

  http.post('/api/claims/:token/verify', async ({ request }) => {
    await delay(500);
    const { code } = (await request.json()) as { code: string };
    if (code === '000000')
      return HttpResponse.json({ errors: { code: ['code-expired'] } }, { status: 410 });
    if (code === '123456') {
      state.verified = true;
      save();
      const invite = INVITE[environment.market === 'ZA' ? 'ZA' : 'IN'];
      mockSession(true);
      return HttpResponse.json({
        id: 'u-claim',
        firstName: invite.name.split(' ')[0],
        phone: invite.sentTo,
      });
    }
    tries -= 1;
    if (tries <= 0)
      return HttpResponse.json({ errors: { code: ['too-many-tries'] } }, { status: 429 });
    return HttpResponse.json(
      { errors: { code: ['invalid-code'], triesLeft: [String(tries)] } },
      { status: 422 },
    );
  }),

  http.post('/api/claims/:token/consent', async () => {
    await delay(700);
    state.consented = true;
    save();
    // Pre-filled quests 1–5 open for editing from V3 (XP is granted when the profile is confirmed).
    mockProgress.completed = Math.max(mockProgress.completed, 5);
    persistQuests();
    return HttpResponse.json(sections());
  }),

  http.post('/api/claims/:token/sections/:slug/confirm', async ({ params }) => {
    await delay(300);
    confirmed.add(String(params['slug']));
    save();
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/claims/:token/finish', async () => {
    await delay(800);
    const [first = '', last = ''] = INVITE[market()].name.split(' ');
    seedAnswers('who-dis', { firstName: first, lastName: last });
    Object.assign(mockProgress, { completed: Math.max(mockProgress.completed, 5), partsDone: 0 });
    mockProgress.xp = Math.max(mockProgress.xp, CLAIMED_XP);
    mockProgress.badges = Math.max(mockProgress.badges, 5);
    syncLevel();
    markClaimed();
    state.result = {
      profilePercent: 80,
      verifiedItems: 2,
      jobs: 9,
      xpLeft: xpOf(6) + xpOf(7) + xpOf(8),
      minutesLeft: 6,
      left: [
        {
          quest: 6,
          slug: 'pick-your-arc',
          name: 'Pick Your Arc',
          blurb: 'Jobs you want, where, when you can start',
          xp: xpOf(6),
        },
        {
          quest: 7,
          slug: 'drop-the-receipts',
          name: 'Drop the Receipts',
          blurb: `Your ${INVITE[market()].certificate} is already verified ✓`,
          xp: xpOf(7),
        },
        {
          quest: 8,
          slug: 'vibe-check',
          name: 'Vibe Check',
          blurb: 'Review and choose who can see you',
          xp: xpOf(8),
        },
      ],
    };
    save();
    return HttpResponse.json(state.result);
  }),

  http.post('/api/claims/:token/decline', async ({ params }) => {
    await delay(600);
    rejected.add(String(params['token']));
    Object.assign(state, { verified: false, consented: false, result: null });
    confirmed.clear();
    save();
    return new HttpResponse(null, { status: 204 });
  }),
];
```

## `src/mocks/contact.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { rememberLogin } from './persona';

/** Phone code: 123456 works. PINs 560038 / 400001 (IN) and postal codes 2194 / 2001 (ZA) are known. */
const POSTAL: Record<string, { region: string; district: string; city: string; areas: string[] }> =
  {
    'IN/560038': {
      region: 'Karnataka',
      district: 'Bengaluru Urban',
      city: 'Bengaluru',
      areas: [
        'Indiranagar',
        'HAL II Stage',
        'Doopanahalli',
        'Jeevan Bima Nagar',
        'New Thippasandra',
        'Domlur',
        'Kodihalli',
      ],
    },
    'IN/400001': {
      region: 'Maharashtra',
      district: 'Mumbai',
      city: 'Mumbai',
      areas: ['Fort', 'Colaba Causeway', 'Ballard Estate'],
    },
    'ZA/2194': {
      region: 'Gauteng',
      district: 'City of Johannesburg',
      city: 'Johannesburg',
      areas: [
        'Randburg (Ferndale)',
        'Randburg Central',
        'Blairgowrie',
        'Bordeaux',
        'Fontainebleau',
      ],
    },
    'ZA/2001': {
      region: 'Gauteng',
      district: 'City of Johannesburg',
      city: 'Johannesburg',
      areas: ['Braamfontein', 'Marshalltown'],
    },
  };

const REGIONS: Record<string, string[]> = {
  IN: [
    'Andhra Pradesh',
    'Arunachal Pradesh',
    'Assam',
    'Bihar',
    'Chhattisgarh',
    'Delhi',
    'Goa',
    'Gujarat',
    'Haryana',
    'Himachal Pradesh',
    'Jammu and Kashmir',
    'Jharkhand',
    'Karnataka',
    'Kerala',
    'Madhya Pradesh',
    'Maharashtra',
    'Manipur',
    'Meghalaya',
    'Mizoram',
    'Nagaland',
    'Odisha',
    'Puducherry',
    'Punjab',
    'Rajasthan',
    'Sikkim',
    'Tamil Nadu',
    'Telangana',
    'Tripura',
    'Uttar Pradesh',
    'Uttarakhand',
    'West Bengal',
  ],
  ZA: [
    'Eastern Cape',
    'Free State',
    'Gauteng',
    'KwaZulu-Natal',
    'Limpopo',
    'Mpumalanga',
    'North West',
    'Northern Cape',
    'Western Cape',
  ],
};

export const contactHandlers = [
  http.post('/api/me/phone/code', async ({ request }) => {
    await delay(400);
    const { phone } = (await request.json()) as { phone: string };
    const d = phone.replace(/\D/g, '');
    return HttpResponse.json({
      sentTo: `+${d.slice(0, d.length - 10)} ${d.slice(-10, -5)} ${d.slice(-5)}`,
      resendIn: 30,
      triesLeft: 5,
    });
  }),

  http.post('/api/me/phone/verify', async ({ request }) => {
    await delay(400);
    const { phone, code } = (await request.json()) as { phone: string; code: string };
    if (code === '123456') {
      rememberLogin(phone); // A-308: a verified mobile can log in next time.
      return new HttpResponse(null, { status: 204 });
    }
    if (code === '000000') {
      return HttpResponse.json(
        { message: 'Expired', errors: { code: ['code-expired'] } },
        { status: 410 },
      );
    }
    return HttpResponse.json(
      { message: 'Wrong', errors: { code: ['invalid-code'], triesLeft: ['4'] } },
      { status: 422 },
    );
  }),

  http.get('/api/geo/:market/postal/:code', async ({ params }) => {
    await delay(350);
    const hit = POSTAL[`${params['market']}/${params['code']}`];
    if (!hit) return HttpResponse.json({ message: 'Not found' }, { status: 404 });
    return HttpResponse.json({
      ...hit,
      areas: hit.areas.map((name) => ({ name, sub: hit.district })),
    });
  }),

  http.get('/api/geo/:market/regions', ({ params }) =>
    HttpResponse.json(REGIONS[String(params['market'])] ?? []),
  ),
];
```

## `src/mocks/core.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';

/** Session + draft autosave. Drafts are kept in memory for the mock session. */
const drafts = new Map<string, unknown>();

export const coreHandlers = [
  http.put('/api/drafts/:key', async ({ params, request }) => {
    await delay(200);
    drafts.set(String(params['key']), await request.json());
    return new HttpResponse(null, { status: 204 });
  }),
  http.get('/api/drafts/:key', ({ params }) => {
    const draft = drafts.get(String(params['key']));
    return draft ? HttpResponse.json(draft) : new HttpResponse(null, { status: 404 });
  }),
];
```

## `src/mocks/cv.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { seedAnswers } from './quest.handlers';

/**
 * Mock CV parser. Name the file to trigger an outcome:
 * "locked" → S3 · "blurry" (image) → S4 · "afrikaans" → S5 · "stuck" → S6 · "nowork" → S7 · "little" → S8 ·
 * "taken" → S9. Anything else parses cleanly in ~6 s.
 */
const jobs = new Map<string, { name: string; started: number }>();
const STEP_MS = 1000;
const QUIPS = [
  'Opening your file…',
  'Got your number. Literally.',
  'Matric (NSC)? Respect.',
  'Ooh, a call-centre internship? Love that for you.',
  'Building your timeline…',
  'Skipping the private stuff, as promised.',
];

function outcomeFor(name: string): { outcome?: string; detail?: string; failed?: boolean } {
  const n = name.toLowerCase();
  if (n.includes('locked')) return { outcome: 'locked-pdf', failed: true };
  if (n.includes('blurry')) return { outcome: 'blurry-photo', failed: true };
  if (n.includes('stuck')) return { outcome: 'parse-failed', detail: 'CV-2291', failed: true };
  if (n.includes('afrikaans')) return { outcome: 'other-language', detail: 'Afrikaans' };
  if (n.includes('nowork')) return { outcome: 'no-work-history' };
  if (n.includes('little')) return { outcome: 'little-found', detail: '4' };
  if (n.includes('taken')) return { outcome: 'email-taken' };
  return {};
}

export const cvHandlers = [
  http.post('/api/cv', async ({ request }) => {
    const form = await request.formData();
    const file = (form.get('file') ?? form.getAll('pages')[0] ?? null) as File | null;
    await delay(700);
    const cvId = `cv-${Math.random().toString(36).slice(2, 8)}`;
    jobs.set(cvId, { name: file?.name ?? 'cv.pdf', started: Date.now() });
    return HttpResponse.json({ cvId });
  }),

  http.get('/api/cv/:id/status', ({ params }) => {
    const job = jobs.get(String(params['id']));
    if (!job) return new HttpResponse(null, { status: 404 });
    const elapsed = Date.now() - job.started;
    const { outcome, detail, failed } = outcomeFor(job.name);
    if (failed && elapsed > STEP_MS * 2) {
      return HttpResponse.json({ state: 'failed', percent: 30, step: 1, outcome, detail });
    }
    const step = Math.min(5, Math.floor(elapsed / STEP_MS));
    const percent = Math.min(100, Math.round((elapsed / (STEP_MS * 6)) * 100));
    if (percent >= 100)
      return HttpResponse.json({ state: 'done', percent: 100, step: 6, outcome, detail });
    return HttpResponse.json({ state: 'parsing', percent, step, quip: QUIPS[step] });
  }),

  http.get('/api/cv/:id/summary', async () => {
    await delay(300);
    seedAnswers('your-flex-era', CV_FLEX);
    seedAnswers('work-lore', CV_LORE);
    return HttpResponse.json({
      questsPrefilled: 5,
      detailsFound: 31,
      toCheck: 3,
      skillsMatched: 9,
      skipped: ['photo', 'ID number', 'marital status'],
      sections: [
        {
          quest: 1,
          title: 'Your name',
          badge: '2 found',
          fields: [
            { key: 'firstName', label: 'First name', value: 'Nkosazana' },
            { key: 'lastName', label: 'Last name', value: 'Dlamini' },
          ],
        },
        {
          quest: 2,
          title: 'Contact & address',
          badge: '4 found · 1 to check',
          fields: [
            { key: 'email', label: 'Email', value: 'nkosazanadlamini@gmail.com' },
            { key: 'mobile', label: 'Mobile', value: '+27 71 234 5678' },
            { key: 'homeLanguage', label: 'Home language', value: 'isiZulu' },
            {
              key: 'area',
              label: 'Area',
              value: 'Randburg, Gauteng',
              check: 'Found a suburb only — add your postal code',
            },
          ],
        },
        {
          quest: 3,
          title: 'School & qualifications',
          badge: '2 found · 1 to check',
          fields: [
            {
              key: 'level',
              label: 'Highest level',
              value: 'Matric (NSC) · Randburg High School · 2021',
            },
            {
              key: 'cert',
              label: 'Certificate',
              value: 'Contact Centre Operations · NQF 4',
              check: 'Year not found — when did you finish?',
            },
          ],
        },
        {
          quest: 4,
          title: 'Skills & languages',
          badge: '9 skills · 3 languages',
          chips: [
            'Customer service',
            'Active listening',
            'MS Excel',
            'Call centre systems',
            'Data capturing',
            'Conflict resolution',
            'Typing',
            'Teamwork',
            'Scheduling',
          ],
          fields: [
            {
              key: 'skillLevels',
              label: 'Skill levels',
              value: 'Guessed from your CV — you’ll confirm each one',
            },
            {
              key: 'languages',
              label: 'Languages',
              value: 'isiZulu (Native) · English (Fluent) · Sesotho',
              check: 'How well do you speak Sesotho?',
            },
          ],
        },
        {
          quest: 5,
          title: 'Work history',
          badge: '2 chapters · 2 yrs 3 mos',
          fields: [
            {
              key: 'job1',
              label: 'Chapter 1',
              value: 'Call centre agent (internship) · Teleperformance · Jan – Jun 2024',
            },
            { key: 'job2', label: 'Chapter 2', value: 'Cashier · Shoprite · Mar 2022 – Dec 2023' },
            { key: 'gap', label: 'Gap', value: 'Jul 2024 – now · shown only to you' },
          ],
        },
      ],
    });
  }),

  http.delete('/api/cv/:id', ({ params }) => {
    jobs.delete(String(params['id']));
    return new HttpResponse(null, { status: 204 });
  }),
];

/** What the mock CV (Nkosazana Dlamini, contact centre) gives Quest 4: 9 matched skills + 1 custom, 3 languages. */
const cvSkill = (id: string, name: string, category: string, level: number) => ({
  id,
  name,
  category,
  level,
  source: 'cv',
});
const CV_FLEX = {
  skills: [
    cvSkill('customer-service', 'Customer service', 'people', 3),
    cvSkill('active-listening', 'Active listening', 'people', 2),
    cvSkill('call-centre-systems', 'Call centre systems', 'tech', 2),
    cvSkill('excel', 'MS Excel', 'data', 2),
    cvSkill('data-capturing', 'Data capturing', 'data', 2),
    cvSkill('conflict-resolution', 'Conflict resolution', 'people', 1),
    cvSkill('communication', 'Communication', 'people', 2),
    cvSkill('problem-solving', 'Problem solving', 'people', 2),
    cvSkill('ms-office', 'MS Office', 'business', 2),
    { id: 'custom-syspro', name: 'Syspro', category: 'custom', level: 0, source: 'cv' },
  ],
  languages: [
    {
      name: 'English',
      fluency: 'fluent',
      speak: true,
      read: true,
      write: true,
      motherTongue: false,
      fromCv: true,
    },
    {
      name: 'Sesotho',
      fluency: '',
      speak: true,
      read: false,
      write: false,
      motherTongue: false,
      fromCv: true,
    },
  ],
};

/** Quest 5 chapters read from the mock CV (Figma 200:31683 "Imported from CV"). */
const CV_LORE = {
  workTypes: ['internship', 'part_time'],
  chapters: [
    {
      id: 'cv1',
      type: 'internship',
      role: 'Call Centre Agent',
      company: 'Teleperformance',
      start: '2024-01',
      end: '2024-06',
      current: false,
      mode: 'onsite',
      skills: ['customer-service', 'call-centre-systems', 'active-listening'],
      description: '',
      fromCv: true,
    },
    {
      id: 'cv2',
      type: 'part_time',
      role: 'Cashier',
      company: 'Shoprite',
      start: '2022-03',
      end: '2023-12',
      current: false,
      mode: 'onsite',
      skills: ['customer-service', 'data-capturing'],
      description: '',
      fromCv: true,
      typeUnsure: true,
    },
  ],
};
```

## `src/mocks/esco.data.ts`

```ts
import type { EscoCategory, EscoSkill, EscoSkillType } from '../app/core/skills/esco.api';

import { SKILLS } from '../app/features/quests/data/skills';

/**
 * Mock of the ESCO skills pillar (A-245, A-285): the curated ESCO v1.2.1 sheet — the same 956
 * skills as the deck tabs — so search and browsing agree. The live server proxies ESCO's search
 * API, which returns real concept URIs; these mock URIs say "mock".
 */
export const ESCO_SKILLS: readonly EscoSkill[] = SKILLS.map((k) => ({
  uri: `http://data.europa.eu/esco/skill/mock-${k.id}`,
  label: k.name,
  type: (k.group.startsWith('T')
    ? 'transversal'
    : k.kind === 'knowledge'
      ? 'knowledge'
      : 'skill') as EscoSkillType,
  category: k.category as EscoCategory,
  escoCategory: k.escoCategory,
  alt: k.synonyms ?? [],
}));

const norm = (t: string) =>
  t
    .toLowerCase()
    .replace(/[^a-z0-9+#]+/g, ' ')
    .trim();

/** ESCO-like ranking: preferred-label prefix, then word prefix, then alternative labels. */
export function escoSearch(text: string, limit: number): { total: number; results: EscoSkill[] } {
  const q = norm(text);
  if (!q) return { total: 0, results: [] };
  const scored = ESCO_SKILLS.map((k) => {
    const label = norm(k.label);
    const alt = (k.alt ?? []).map(norm);
    const score = label.startsWith(q)
      ? 4
      : label.split(' ').some((w) => w.startsWith(q))
        ? 3
        : alt.some((a) => a === q)
          ? 2
          : alt.some((a) => a.startsWith(q) || a.split(' ').some((w) => w.startsWith(q)))
            ? 1
            : 0;
    return { k, score };
  })
    .filter((x) => x.score > 0)
    .sort((a, b) => b.score - a.score || a.k.label.localeCompare(b.k.label));
  return { total: scored.length, results: scored.slice(0, limit).map((x) => x.k) };
}
```

## `src/mocks/feedback.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { creditXp } from './xp-ledger';
import { syncLevel } from './quest.handlers';

/** A-274: +5 XP for feedback, at most once every 30 days. */
const KEY = 'sm_mock_feedback';
const XP = 5;
const MONTH = 30 * 86_400_000;

export const feedbackHandlers = [
  http.post('/api/me/feedback', async () => {
    await delay(400);
    let last = 0;
    try {
      last = Number(sessionStorage.getItem(KEY) ?? 0);
    } catch {
      /* private mode */
    }
    const now = Date.now();
    const xp = now - last >= MONTH ? XP : 0;
    if (xp) {
      creditXp(xp);
      syncLevel();
      try {
        sessionStorage.setItem(KEY, String(now));
      } catch {
        /* private mode */
      }
    }
    return HttpResponse.json({ xp });
  }),
];
```

## `src/mocks/fest.handlers.ts`

```ts
import { profileProgress } from './profile-progress';
import { HttpResponse, delay, http } from 'msw';
import { isNewLearner } from './persona';
import type {
  BookRequest,
  BookedInterview,
  Booking,
  Fest,
  FestAlerts,
  FestEmployer,
  Outcome,
  PastFest,
  Reminders,
  Slot,
  SlotGrid,
} from '../app/core/fest/fest.api';
import { recordFestOffer } from './jobs.handlers';

/**
 * Hire Fest v2 (Figma 315:44812, handoff 318:55923). Four upcoming fests, per-employer 30-min slot
 * grid, one ticket per fest, waitlist, day-of live queue and past outcomes. Mock "today" is
 * 28 Nov 2026 so the countdowns match the frames. Page URL scenarios (`?hf=`): locked (H10) ·
 * none (H11) · live (H12) · error.
 */

const KEY = 'sm_mock_fest';
const TODAY = Date.UTC(2026, 10, 28);
const MINUTES = 30;
const BUFFER = 30;
const MAX_EMPLOYERS = 2;

function scenario(): string {
  return typeof location === 'undefined'
    ? ''
    : (new URLSearchParams(location.search).get('hf') ?? '');
}

const E = (
  id: string,
  company: string,
  initials: string,
  role: string,
  openings: number,
  slotsLeft: number,
  days: string,
  match: number,
): FestEmployer => ({
  id,
  company,
  initials,
  role,
  openings,
  slotsLeft,
  days,
  match,
  full: slotsLeft === 0,
});

const BASE: Omit<Fest, 'booked' | 'saved'>[] = [
  {
    id: 'tech-bpo-pune',
    name: 'Tech & BPO Hire Fest',
    mode: 'in-person',
    city: 'Pune',
    start: '2026-12-01',
    end: '2026-12-03',
    dateLabel: '1 – 3 Dec 2026',
    hours: '10:00 am – 6:00 pm · 30-min interviews',
    venue: 'Pune IT Park, Hall B, Hinjewadi',
    venueNote: 'Entry from Gate 2',
    distance: '12 km from you',
    organiser: 'SkillMuni Careers',
    employers: [
      E('cg', 'Cognizant', 'CG', 'Customer Support', 12, 6, 'Day 1–3', 94),
      E('tp', 'Teleperformance', 'TP', 'Voice Process Exec', 10, 11, 'Day 1–2', 90),
      E('hl', 'HDFC Life', 'HL', 'Inbound Sales Exec', 6, 14, 'Day 2–3', 82),
      E('cx', 'Concentrix', 'CX', 'Tech Support Trainee', 8, 0, 'Day 1–3', 76),
      E('il', 'ICICI Lombard', 'IL', 'Claims Support', 4, 9, 'Day 1', 71),
      E('ta', 'Tata AIG', 'TA', 'Customer Care Exec', 5, 7, 'Day 2', 68),
      E('ax', 'Axis Bank', 'AX', 'Relationship Officer', 6, 12, 'Day 3', 63),
      E('wi', 'Wipro', 'WI', 'Service Desk Analyst', 7, 8, 'Day 1–2', 58),
    ],
    employerCount: 8,
    roleCount: 46,
    price: 'Free',
    free: true,
    seatsLeft: 10,
    matchingRoles: 5,
    bookingCloses: '30 Nov, 6 pm',
    cancelPolicy: 'Cancel anytime up to 24 hrs before',
    beforeYouGo: [
      'Your CV is attached automatically from My Job Profile',
      'Photo ID + this ticket (QR) at the entry desk',
      'Dress: smart casual · arrive 15 min early',
    ],
    waitlistOpen: false,
  },
  {
    id: 'online-cs',
    name: 'Online Hire Fest: Customer Support',
    mode: 'online',
    city: 'Online',
    start: '2027-01-14',
    end: '2027-01-14',
    dateLabel: 'Wed, 14 Jan 2027',
    hours: '10:00 am – 6:00 pm IST · 30-min video interviews',
    venue: 'Online on SkillMuni Meet',
    venueNote: 'Join link + test call unlock after you book',
    distance: null,
    organiser: 'SkillMuni Careers',
    employers: [
      E('hl', 'HDFC Life', 'HL', 'Inbound Sales Exec', 6, 14, 'Day 1', 92),
      E('il', 'ICICI Lombard', 'IL', 'Claims Support', 4, 9, 'Day 1', 88),
      E('ta', 'Tata AIG', 'TA', 'Customer Care Exec', 5, 20, 'Day 1', 81),
      E('tp', 'Teleperformance', 'TP', 'Chat Support Exec', 15, 18, 'Day 1', 79),
      E('zo', 'Zomato', 'ZO', 'Customer Delight', 10, 16, 'Day 1', 74),
      E('az', 'Amazon', 'AZ', 'Virtual Customer Service', 20, 25, 'Day 1', 72),
      E('at', 'Airtel', 'AT', 'Customer Service', 8, 10, 'Day 1', 70),
      E('ib', 'Infosys BPM', 'IB', 'Process Executive', 12, 14, 'Day 1', 66),
      E('qc', 'Quess Corp', 'QC', 'Back Office', 6, 9, 'Day 1', 61),
      E('ug', 'upGrad', 'UG', 'Learner Support', 4, 6, 'Day 1', 58),
    ],
    employerCount: 10,
    roleCount: 32,
    price: 'Free',
    free: true,
    seatsLeft: 120,
    matchingRoles: 8,
    bookingCloses: '13 Jan, 6 pm',
    cancelPolicy: 'Cancel anytime up to 24 hrs before',
    beforeYouGo: [
      'Your CV is attached automatically from My Job Profile',
      'Quiet spot, earphones, 1 Mbps+ internet',
      'Do the 1-min test call the day before',
    ],
    waitlistOpen: false,
  },
  {
    id: 'retail-mumbai',
    name: 'Retail & Hospitality Hire Fest',
    mode: 'in-person',
    city: 'Mumbai',
    start: '2026-12-20',
    end: '2026-12-20',
    dateLabel: 'Sun, 20 Dec 2026',
    hours: '10:00 am – 5:00 pm · 30-min interviews',
    venue: 'NESCO Centre, Goregaon',
    venueNote: 'Hall 3, ground floor',
    distance: '148 km from you',
    organiser: 'SkillMuni Careers',
    employers: [
      E('rt', 'Reliance Trends', 'RT', 'Store Associate', 14, 8, 'Day 1', 81),
      E('tj', 'Taj Hotels', 'TJ', 'Front Desk Associate', 6, 5, 'Day 1', 77),
      E('dm', 'DMart', 'DM', 'Customer Associate', 20, 12, 'Day 1', 72),
      E('lf', 'Lifestyle', 'LF', 'Retail Associate', 10, 9, 'Day 1', 64),
    ],
    employerCount: 6,
    roleCount: 28,
    price: 'Free',
    free: true,
    seatsLeft: 34,
    matchingRoles: 3,
    bookingCloses: '18 Dec, 6 pm',
    cancelPolicy: 'Cancel anytime up to 24 hrs before',
    beforeYouGo: [
      'Your CV is attached automatically from My Job Profile',
      'Photo ID + this ticket (QR) at the entry desk',
      'Dress: smart casual · arrive 15 min early',
    ],
    waitlistOpen: false,
  },
  {
    id: 'fintech-blr',
    name: 'Fintech Careers Day',
    mode: 'in-person',
    city: 'Bengaluru',
    start: '2027-01-28',
    end: '2027-01-28',
    dateLabel: 'Thu, 28 Jan 2027',
    hours: '10:00 am – 5:00 pm · 30-min interviews',
    venue: 'KTPO Convention Centre, Whitefield',
    venueNote: 'Hall A',
    distance: '840 km from you',
    organiser: 'FinSkills Foundation',
    employers: [
      E('pz', 'PhonePe', 'PZ', 'Merchant Support', 6, 0, 'Day 1', 66),
      E('rz', 'Razorpay', 'RZ', 'Customer Success Associate', 4, 0, 'Day 1', 62),
      E('cr', 'CRED', 'CR', 'Member Support', 3, 0, 'Day 1', 55),
    ],
    employerCount: 5,
    roleCount: 18,
    price: '₹199',
    free: false,
    seatsLeft: 0,
    matchingRoles: 1,
    bookingCloses: '26 Jan, 6 pm',
    cancelPolicy: 'Full refund up to 24 hrs before',
    beforeYouGo: [
      'Your CV is attached automatically from My Job Profile',
      'Photo ID + this ticket (QR) at the entry desk',
      'Dress: business casual',
    ],
    waitlistOpen: true,
  },
];

/* ---------- State ---------- */

interface Pick {
  employerId: string;
  date: string;
  time: string;
}

interface StoredBooking {
  id: string;
  code: string;
  festId: string;
  picks: Pick[];
  reminders: Reminders;
  cv: string;
}

interface State {
  bookings: StoredBooking[];
  saved: string[];
  waitlist: Record<string, number>;
  outcomes: Record<string, Outcome>;
  ratings: Record<string, number>;
  alerts: FestAlerts;
}

const SEED_BOOKING: StoredBooking = {
  id: 'bk-retail',
  code: 'HF-3MRB-0420',
  festId: 'retail-mumbai',
  picks: [{ employerId: 'rt', date: '2026-12-20', time: '11:00' }],
  reminders: { whatsapp: true, sms: true, calendar: true },
  cv: 'Yash_Pohare_CV.pdf · Creative',
};

function load(): State {
  const base: State = {
    bookings: isNewLearner() ? [] : [SEED_BOOKING],
    saved: [],
    waitlist: {},
    outcomes: isNewLearner() ? {} : { 'pune-fresher:hl': 'offer' },
    ratings: isNewLearner() ? {} : { 'pune-fresher': 4 },
    alerts: { city: true, online: true },
  };
  if (scenario() === 'live')
    base.bookings = [
      {
        id: 'bk-live',
        code: 'HF-7Q4X-1201',
        festId: 'tech-bpo-pune',
        picks: [
          { employerId: 'cg', date: '2026-12-01', time: '10:30' },
          { employerId: 'tp', date: '2026-12-01', time: '12:00' },
        ],
        reminders: { whatsapp: true, sms: true, calendar: true },
        cv: 'Yash_Pohare_CV.pdf · Creative',
      },
      SEED_BOOKING,
    ];
  try {
    const raw = sessionStorage.getItem(KEY + scenario());
    return raw ? { ...base, ...(JSON.parse(raw) as Partial<State>) } : base;
  } catch {
    return base;
  }
}

/** Whether the learner holds a booking (A-306 / A-321: the "Book a Hire Fest slot" mission). */
export function festActivity(): { booked: boolean } {
  return { booked: load().bookings.length > 0 };
}

function save(s: State): void {
  try {
    sessionStorage.setItem(KEY + scenario(), JSON.stringify(s));
  } catch {
    /* private mode */
  }
}

/* ---------- Helpers ---------- */

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const WEEK = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function day(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y ?? 2026, (m ?? 1) - 1, d ?? 1));
}

const dayLabel = (iso: string) => {
  const d = day(iso);
  return `${WEEK[d.getUTCDay()]} ${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}`;
};

function dates(f: { start: string; end: string }): string[] {
  const out: string[] = [];
  for (let t = day(f.start).getTime(); t <= day(f.end).getTime(); t += 86_400_000)
    out.push(new Date(t).toISOString().slice(0, 10));
  return out;
}

const clock = (t: string) => {
  const [h = 0, m = 0] = t.split(':').map(Number);
  const pm = h >= 12;
  const hh = h % 12 || 12;
  return `${hh}:${String(m).padStart(2, '0')} ${pm ? 'pm' : 'am'}`;
};

const plus = (t: string, min: number) => {
  const [h = 0, m = 0] = t.split(':').map(Number);
  const total = h * 60 + m + min;
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
};

const range = (t: string) => {
  const end = plus(t, MINUTES);
  const ap = (x: string) => (Number(x.split(':')[0]) >= 12 ? 'pm' : 'am');
  return `${clock(t).replace(/ (am|pm)$/, '')} – ${clock(end).replace(/ (am|pm)$/, '')} ${ap(end)}`;
};

/** Deterministic seats per slot so the grid is the same on every load. */
function seats(festId: string, emp: string, date: string, time: string): number {
  let h = 0;
  for (const c of `${festId}${emp}${date}${time}`) h = (h * 31 + c.charCodeAt(0)) % 9973;
  return h % 7 === 0 ? 0 : (h % 6) + 1;
}

const TIMES: Record<string, string[]> = {
  cg: ['10:00', '10:30', '11:00', '11:30', '14:00', '14:30'],
  tp: ['10:30', '11:00', '12:00', '12:30', '15:00', '15:30'],
};
const DEFAULT_TIMES = ['10:00', '11:00', '11:30', '12:30', '14:30', '16:00'];

function slotsFor(festId: string, empId: string, date: string): Slot[] {
  const fixed: Record<string, Record<string, number>> = {
    cg: { '10:00': 2, '10:30': 3, '11:00': 0, '11:30': 4, '14:00': 3, '14:30': 6 },
    tp: { '10:30': 2, '11:00': 5, '12:00': 3, '12:30': 2, '15:00': 0, '15:30': 4 },
  };
  return (TIMES[empId] ?? DEFAULT_TIMES).map((time) => {
    const left =
      festId === 'tech-bpo-pune' && date === '2026-12-01' && fixed[empId]
        ? (fixed[empId]?.[time] ?? 1)
        : seats(festId, empId, date, time);
    return { time, label: clock(time), left };
  });
}

function fest(f: (typeof BASE)[number], s: State): Fest {
  return { ...f, booked: s.bookings.some((b) => b.festId === f.id), saved: s.saved.includes(f.id) };
}

let deskSeed = 3;
function booking(b: StoredBooking, s: State): Booking {
  const f = BASE.find((x) => x.id === b.festId) ?? BASE[0];
  const desks: Record<string, string> = { cg: 'Desk 4', tp: 'Desk 9', rt: 'Desk 2' };
  const hall =
    f?.mode === 'online' ? 'Online room' : f?.venue.includes('Hall B') ? 'Hall B' : 'Hall 3';
  const interviews: BookedInterview[] = b.picks.map((p) => {
    const e = f?.employers.find((x) => x.id === p.employerId);
    return {
      employerId: p.employerId,
      company: e?.company ?? p.employerId,
      initials: e?.initials ?? '··',
      role: e?.role ?? '',
      date: p.date,
      dayLabel: dayLabel(p.date),
      time: clock(p.time),
      range: range(p.time),
      desk:
        f?.mode === 'online'
          ? 'Video call'
          : `${hall}, ${desks[p.employerId] ?? `Desk ${(deskSeed++ % 12) + 1}`}`,
    };
  });
  const first = b.picks.map((p) => p.date).sort()[0] ?? f?.start ?? '2026-12-01';
  const live =
    scenario() === 'live' && b.festId === 'tech-bpo-pune'
      ? {
          checkedInAt: '10:12',
          current: { employerId: 'cg', position: 3, waitMinutes: 15, lateBy: 10, progress: 0.6 },
        }
      : null;
  return {
    id: b.id,
    code: b.code,
    fest: fest(f as (typeof BASE)[number], s),
    interviews,
    daysToGo: live ? 0 : Math.max(0, Math.round((day(first).getTime() - TODAY) / 86_400_000)),
    reminders: b.reminders,
    cv: b.cv,
    live,
  };
}

function past(s: State): PastFest[] {
  if (isNewLearner()) return [];
  const rows = [
    {
      id: 'pune-fresher',
      name: 'Pune Fresher Hire Fest',
      date: '2026-11-08',
      interviews: [
        { employerId: 'hl', company: 'HDFC Life', initials: 'HL', role: 'Inbound Sales Exec' },
        { employerId: 'il', company: 'ICICI Lombard', initials: 'IL', role: 'Claims Support' },
      ],
    },
    {
      id: 'online-sept',
      name: 'Online Career Day: Freshers',
      date: '2026-09-19',
      interviews: [
        { employerId: 'ib', company: 'Infosys BPM', initials: 'IB', role: 'Process Executive' },
      ],
    },
  ];
  return rows.map((r) => {
    const d = day(r.date);
    return {
      id: r.id,
      name: r.name,
      date: r.date,
      day: String(d.getUTCDate()).padStart(2, '0'),
      month: (MONTHS[d.getUTCMonth()] ?? '').toUpperCase(),
      year: String(d.getUTCFullYear()),
      rating: s.ratings[r.id] ?? null,
      interviews: r.interviews.map((i) => ({
        ...i,
        outcome: s.outcomes[`${r.id}:${i.employerId}`] ?? null,
      })),
    };
  });
}

const fail = () => (scenario() === 'error' ? new HttpResponse(null, { status: 503 }) : null);
const code = () =>
  `HF-${Math.random().toString(36).slice(2, 6).toUpperCase()}-${String(Math.floor(1000 + Math.random() * 9000))}`;

/** A-262: booking needs a 60% job profile; what's left comes from the quests' skipped steps. */
function festGate() {
  if (scenario() === 'locked')
    return {
      profilePercent: 45,
      unlockAt: 60,
      missing: [
        { title: 'Your Flex Era · skills', detail: '3 min', xp: 0, link: '/quests/your-flex-era' },
        { title: 'Work Lore', detail: '2 min · or say you’re a fresher', xp: 0, link: '/quests/work-lore' },
      ],
    };
  const p = profileProgress();
  return {
    profilePercent: p.percent,
    unlockAt: 60,
    missing: p.quests
      .filter((q) => q.left.length)
      .map((q) => ({
        title: `${q.name} · ${q.left.map((l) => l.label).join(', ')}`,
        detail: q.left.length === 1 ? '1 step left' : `${q.left.length} steps left`,
        xp: 0,
        link: `/quests/${q.slug}`,
      })),
  };
}

export const festHandlers = [
  http.get('/api/fests/summary', async () => {
    await delay(200);
    const err = fail();
    if (err) return err;
    const s = load();
    return HttpResponse.json({
      upcoming: scenario() === 'none' ? 0 : BASE.length,
      bookings: s.bookings.length,
      past: isNewLearner() ? 0 : 2,
      ...festGate(),
      city: 'Pune',
    });
  }),

  http.get('/api/fests', async ({ request }) => {
    await delay(400);
    const err = fail();
    if (err) return err;
    const s = load();
    if (scenario() === 'none') return HttpResponse.json([]);
    const filter = new URL(request.url).searchParams.get('filter') ?? 'all';
    const list = BASE.map((f) => fest(f, s)).filter((f) =>
      filter === 'free'
        ? f.free
        : filter === 'in-person'
          ? f.mode === 'in-person'
          : filter === 'online'
            ? f.mode === 'online'
            : filter === 'city'
              ? f.city === 'Pune'
              : true,
    );
    // Recommended order comes from the server (profile match, distance, date), as drawn in H1.
    return HttpResponse.json(list);
  }),

  http.get('/api/fests/bookings', async () => {
    await delay(300);
    const err = fail();
    if (err) return err;
    const s = load();
    return HttpResponse.json(s.bookings.map((b) => booking(b, s)));
  }),

  http.get('/api/fests/bookings/:id', async ({ params }) => {
    await delay(250);
    const s = load();
    const b = s.bookings.find((x) => x.id === params['id']);
    return b ? HttpResponse.json(booking(b, s)) : new HttpResponse(null, { status: 404 });
  }),

  http.post('/api/fests/bookings/:id/cancel', async ({ params, request }) => {
    await delay(500);
    const { employerId } = (await request.json()) as { employerId: string | null };
    const s = load();
    const b = s.bookings.find((x) => x.id === params['id']);
    if (!b) return new HttpResponse(null, { status: 404 });
    if (employerId) b.picks = b.picks.filter((p) => p.employerId !== employerId);
    if (!employerId || !b.picks.length) s.bookings = s.bookings.filter((x) => x.id !== b.id);
    save(s);
    const left = s.bookings.find((x) => x.id === b.id);
    return HttpResponse.json(left ? booking(left, s) : null);
  }),

  http.get('/api/fests/past', async () => {
    await delay(300);
    const err = fail();
    if (err) return err;
    return HttpResponse.json(past(load()));
  }),

  http.put('/api/fests/past/:id/outcome', async ({ params, request }) => {
    await delay(250);
    const { employerId, outcome } = (await request.json()) as {
      employerId: string;
      outcome: Outcome;
    };
    const s = load();
    s.outcomes[`${String(params['id'])}:${employerId}`] = outcome;
    save(s);
    const p = past(s).find((x) => x.id === params['id']);
    const row = p?.interviews.find((i) => i.employerId === employerId);
    if (outcome === 'offer' && row) recordFestOffer(row.company, row.role);
    return p ? HttpResponse.json(p) : new HttpResponse(null, { status: 404 });
  }),

  http.put('/api/fests/past/:id/rating', async ({ params, request }) => {
    await delay(200);
    const { stars } = (await request.json()) as { stars: number };
    const s = load();
    s.ratings[String(params['id'])] = stars;
    save(s);
    return HttpResponse.json(past(s).find((x) => x.id === params['id']));
  }),

  http.get('/api/fests/alerts', async () => {
    await delay(150);
    return HttpResponse.json(load().alerts);
  }),

  http.put('/api/fests/alerts', async ({ request }) => {
    await delay(200);
    const s = load();
    s.alerts = (await request.json()) as FestAlerts;
    save(s);
    return HttpResponse.json(s.alerts);
  }),

  http.get('/api/fests/:id', async ({ params }) => {
    await delay(250);
    const f = BASE.find((x) => x.id === params['id']);
    return f ? HttpResponse.json(fest(f, load())) : new HttpResponse(null, { status: 404 });
  }),

  http.put('/api/fests/:id/saved', async ({ params, request }) => {
    await delay(150);
    const id = String(params['id']);
    const { saved } = (await request.json()) as { saved: boolean };
    const s = load();
    s.saved = saved ? [...new Set([...s.saved, id])] : s.saved.filter((x) => x !== id);
    save(s);
    const f = BASE.find((x) => x.id === id);
    return f ? HttpResponse.json(fest(f, s)) : new HttpResponse(null, { status: 404 });
  }),

  http.get('/api/fests/:id/slots', async ({ params, request }) => {
    await delay(350);
    const f = BASE.find((x) => x.id === params['id']);
    if (!f) return new HttpResponse(null, { status: 404 });
    const ids = (new URL(request.url).searchParams.get('employers') ?? '')
      .split(',')
      .filter(Boolean);
    const ds = dates(f);
    const slots: SlotGrid['slots'] = Object.fromEntries(
      ids.map((e) => [e, Object.fromEntries(ds.map((d) => [d, slotsFor(f.id, e, d)]))]),
    );
    const days = ds.map((d) => {
      const total = ids.reduce(
        (t, e) => t + (slots[e]?.[d] ?? []).reduce((n, x) => n + x.left, 0),
        0,
      );
      return { date: d, label: dayLabel(d), fewLeft: total < 12 };
    });
    const grid: SlotGrid = {
      days,
      slots,
      minutes: MINUTES,
      buffer: BUFFER,
      maxEmployers: MAX_EMPLOYERS,
    };
    return HttpResponse.json(grid);
  }),

  http.post('/api/fests/:id/book', async ({ params, request }) => {
    await delay(800);
    if (typeof navigator !== 'undefined' && !navigator.onLine) return HttpResponse.error();
    const id = String(params['id']);
    const body = (await request.json()) as BookRequest;
    const s = load();
    const existing = s.bookings.find((b) => b.festId === id);
    if (existing) {
      // Rescheduling or adding: replace the picks for those employers.
      const others = existing.picks.filter(
        (p) => !body.picks.some((n) => n.employerId === p.employerId),
      );
      existing.picks = [...others, ...body.picks];
      existing.reminders = body.reminders;
    } else {
      s.bookings.unshift({
        id: `bk-${id}`,
        code: id === 'tech-bpo-pune' ? 'HF-7Q4X-1201' : code(),
        festId: id,
        picks: [...body.picks],
        reminders: body.reminders,
        cv: `Yash_Pohare_CV.pdf · ${body.cvTemplate}`,
      });
    }
    save(s);
    const b = s.bookings.find((x) => x.festId === id);
    return b ? HttpResponse.json(booking(b, s)) : new HttpResponse(null, { status: 500 });
  }),

  http.get('/api/fests/:id/waitlist', async ({ params, request }) => {
    await delay(200);
    const emp = new URL(request.url).searchParams.get('employerId') ?? '';
    const s = load();
    const pos = s.waitlist[`${String(params['id'])}:${emp}`] ?? 4;
    return HttpResponse.json({
      position: pos,
      odds: 'Usually 1 in 3 waitlisted learners get a slot',
    });
  }),

  http.post('/api/fests/:id/waitlist', async ({ params, request }) => {
    await delay(400);
    const { employerId } = (await request.json()) as { employerId: string };
    const s = load();
    s.waitlist[`${String(params['id'])}:${employerId}`] = 4;
    save(s);
    return HttpResponse.json({
      position: 4,
      odds: 'Usually 1 in 3 waitlisted learners get a slot',
    });
  }),
];
```

## `src/mocks/flex.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { escoSearch } from './esco.data';

/**
 * Q4 trophy mocks. Link check: URLs containing "verify" or "credential" match the learner's name
 * (→ Verified); "fail" times out. Upload: any file. DigiLocker returns two Skill India / NPTEL certificates.
 */
export const flexHandlers = [
  /** ESCO skill search proxy (A-245). `?q=esco-down` fails so the offline fallback can be seen. */
  http.get('/api/skills/esco', async ({ request }) => {
    const url = new URL(request.url);
    const q = url.searchParams.get('q') ?? '';
    await delay(220);
    if (q.includes('esco-down')) return new HttpResponse(null, { status: 503 });
    return HttpResponse.json(escoSearch(q, Number(url.searchParams.get('limit')) || 24));
  }),

  http.post('/api/me/trophies/check-link', async ({ request }) => {
    const { url } = (await request.json()) as { url: string };
    await delay(url.includes('fail') ? 5200 : 900);
    if (url.includes('fail')) return new HttpResponse(null, { status: 504 });
    return HttpResponse.json({ verified: /verify|credential/i.test(url) });
  }),

  http.post('/api/me/trophies/files', async () => {
    await delay(800);
    return HttpResponse.json({ fileId: `f_${Date.now().toString(36)}` });
  }),

  http.post('/api/me/trophies/digilocker', async () => {
    await delay(1200);
    return HttpResponse.json([
      {
        name: 'NPTEL Programming in Java',
        issuer: 'NPTEL · IIT Kharagpur',
        issuedOn: '2023-10',
        credentialId: 'NPTEL23CS74S4',
      },
      {
        name: 'Skill India: Customer Care Executive',
        issuer: 'NSDC · Skill India',
        issuedOn: '2024-03',
        credentialId: 'SI-CCE-88213',
      },
    ]);
  }),
];
```

## `src/mocks/handlers.ts`

```ts
import type { RequestHandler } from 'msw';
import { authHandlers } from './auth.handlers';
import { coreHandlers } from './core.handlers';
import { cvHandlers } from './cv.handlers';
import { questHandlers } from './quest.handlers';
import { contactHandlers } from './contact.handlers';
import { flexHandlers } from './flex.handlers';
import { matchesHandlers } from './matches.handlers';
import { programmeHandlers } from './programme.handlers';
import { claimHandlers } from './claim.handlers';
import { homeHandlers } from './home.handlers';
import { arenaHandlers } from './arena.handlers';
import { learnHandlers } from './learn.handlers';
import { muniHandlers } from './muni.handlers';
import { profileHandlers } from './profile.handlers';
import { statsHandlers } from './stats.handlers';
import { launchpadHandlers } from './launchpad.handlers';
import { jobProfileHandlers } from './job-profile.handlers';
import { assessmentHandlers } from './assessment.handlers';
import { jobsHandlers } from './jobs.handlers';
import { festHandlers } from './fest.handlers';
import { identityHandlers } from './identity.handlers';
import { feedbackHandlers } from './feedback.handlers';

/**
 * Mock API handlers, one file per feature (added with each slice), e.g. `./auth.handlers`.
 * The UI talks to typed API clients behind interfaces, so swapping to the real API needs no UI change.
 */
export const handlers: RequestHandler[] = [
  ...coreHandlers,
  ...authHandlers,
  ...cvHandlers,
  ...questHandlers,
  ...contactHandlers,
  ...flexHandlers,
  ...matchesHandlers,
  ...programmeHandlers,
  ...claimHandlers,
  ...homeHandlers,
  ...arenaHandlers,
  ...learnHandlers,
  ...muniHandlers,
  ...profileHandlers,
  ...statsHandlers,
  ...launchpadHandlers,
  ...jobProfileHandlers,
  ...assessmentHandlers,
  ...jobsHandlers,
  ...festHandlers,
  ...identityHandlers,
  ...feedbackHandlers,
];
```

## `src/mocks/home.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { creditXp } from './xp-ledger';
import { assessmentSnapshot } from './assessment.handlers';
import { isNewLearner, onPersona, sceneOr } from './persona';
import { syncLevel } from './quest.handlers';

/**
 * Lobby feed (Figma Home v2 185:8307). Static content from the design; art and copy will come from the CMS.
 * `?home=empty` in the page URL hides "Pick up where you left off" (new learner); `?home=error` fails the call.
 */
const A = 'assets/home/';

const tile = (slug: string, title: string) => ({
  slug,
  title,
  art: `${A}tile-${slug}.png`,
  link: `/learn/${slug}`,
});

function daypart(): 'morning' | 'afternoon' | 'evening' {
  const h = new Date().getHours();
  return h < 12 ? 'morning' : h < 17 ? 'afternoon' : 'evening';
}

function scenario(): string {
  return sceneOr('home', 'empty');
}

/** A-266: first moves done by visiting them, and whether the +50 bonus was collected. */
const FIRST_KEY = 'sm_mock_first_moves';
const FIRST_MOVES_XP = 50;
function firstState(): { visited: string[]; claimed: boolean } {
  try {
    return { visited: [], claimed: false, ...JSON.parse(sessionStorage.getItem(FIRST_KEY) ?? '{}') };
  } catch {
    return { visited: [], claimed: false };
  }
}
function saveFirst(state: { visited: string[]; claimed: boolean }): void {
  try {
    sessionStorage.setItem(FIRST_KEY, JSON.stringify(state));
  } catch {
    /* private mode */
  }
}
onPersona((p) => {
  if (p === 'new') saveFirst({ visited: [], claimed: false });
});

/** A-233: the three guided first moves for a brand-new learner. */
function firstSteps() {
  let learned = false;
  try {
    const raw = sessionStorage.getItem('sm_mock_learn');
    learned =
      !!raw &&
      Object.values((JSON.parse(raw) as { passed: Record<string, number[]> }).passed).some(
        (p) => p.length,
      );
  } catch {
    /* private mode */
  }
  const ca = assessmentSnapshot();
  // `?first=done` in the page URL: every move done (demos and tests of the bonus, A-266).
  const all = new URLSearchParams(location.search).get('first') === 'done';
  return [
    {
      id: 'assessment',
      title: 'Discover your Career Drivers',
      meta:
        ca && ca.answered && !ca.done
          ? `${ca.answered} of 36 picked · pick up where you left off`
          : '36 quick this-or-that picks · 6 min',
      xp: 100,
      link: '/assessments/career',
      done: all || !!ca?.done,
    },
    {
      id: 'learn',
      title: 'Start your first learning module',
      meta: 'A 2-minute brief, then a quick quiz · XP for every right answer',
      xp: null,
      link: '/learn/brain-vita',
      done: all || learned,
    },
    {
      id: 'jobs',
      title: 'See jobs that fit you',
      meta: 'Matched to what you told us in your quests',
      xp: null,
      link: '/jobs/matching',
      done: all || firstState().visited.includes('jobs'),
    },
  ];
}

function firstMoves() {
  if (!isNewLearner()) return { firstSteps: null, firstMovesBonus: null };
  const state = firstState();
  // Once the bonus is collected the card has done its job and makes way for the usual Lobby.
  if (state.claimed) return { firstSteps: null, firstMovesBonus: null };
  return { firstSteps: firstSteps(), firstMovesBonus: { xp: FIRST_MOVES_XP, claimed: false } };
}

export const homeHandlers = [
  http.post('/api/home/first-steps/:id', async ({ params }) => {
    await delay(120);
    const state = firstState();
    const id = String(params['id']);
    if (!state.visited.includes(id)) saveFirst({ ...state, visited: [...state.visited, id] });
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/home/first-moves/claim', async () => {
    await delay(300);
    const state = firstState();
    const steps = firstSteps();
    if (state.claimed || steps.some((s) => !s.done))
      return HttpResponse.json({ xp: 0 }, { status: state.claimed ? 200 : 409 });
    saveFirst({ ...state, claimed: true });
    creditXp(FIRST_MOVES_XP);
    syncLevel();
    return HttpResponse.json({ xp: FIRST_MOVES_XP });
  }),

  http.get('/api/home', async () => {
    await delay(500);
    const mode = scenario();
    if (mode === 'error') return new HttpResponse(null, { status: 503 });
    return HttpResponse.json({
      daypart: daypart(),
      ...firstMoves(),
      resume:
        mode === 'empty'
          ? null
          : {
              module: 'Brain Vita',
              topic: 'Non-Verbal Reasoning',
              brief: 11,
              briefs: 11,
              note: '1 quiz away from 100% mastery',
              percent: 99,
              xp: 1,
              art: `${A}tile-brain-vita.png`,
              link: '/learn/brain-vita/non-verbal-reasoning',
            },
      pathways: [
        {
          slug: 'career-launchpad',
          title: 'Career Launchpad',
          blurb:
            'Take assessments to match jobs with your skills and needs. Opportunities are waiting!',
          tone: 'arcane',
          art: `${A}pathway-career.png`,
          link: '/launchpad',
        },
        {
          slug: 'grad-gateway',
          title: 'Grad Gateway',
          blurb:
            'Compare countries, find top institutes and register your interest. We’ll guide you all the way!',
          tone: 'ember',
          art: `${A}pathway-grad.png`,
          link: null,
        },
        {
          slug: 'startup-street',
          title: 'Startup Street',
          blurb:
            'Test your Entrepreneurial Quotient, share ideas, find collaborators and explore new paths!',
          tone: 'success',
          art: `${A}pathway-startup.png`,
          link: null,
        },
      ],
      knooks: [
        {
          slug: 'skill',
          title: 'Skill Knook',
          icon: `${A}knook-skill.png`,
          expanded: true,
          modules: [
            tile('brain-vita', 'Brain Vita'),
            tile('english-vinglish', 'English Vinglish'),
            tile('its-my-style', 'It’s My Style'),
            tile('happiness-barometer', 'Happiness Barometer'),
            tile('sharp-shooter', 'Sharp Shooter'),
            tile('surgical-strike', 'Surgical Strike'),
            // A-255: more than six, so "Show more" has something to show.
            { ...tile('banking', 'Money Smart'), slug: 'money-smart', link: '/learn/money-smart' },
            { ...tile('english-vinglish', 'Speak Up'), slug: 'speak-up', link: '/learn/speak-up' },
            {
              ...tile('happiness-barometer', 'Time Boss'),
              slug: 'time-boss',
              link: '/learn/time-boss',
            },
            {
              ...tile('it-gyan', 'Digital Detox'),
              slug: 'digital-detox',
              link: '/learn/digital-detox',
            },
          ],
        },
        {
          slug: 'industry',
          title: 'Industry Knook',
          icon: `${A}knook-industry.png`,
          expanded: true,
          modules: [
            tile('insurance', 'Insurance'),
            tile('banking', 'Banking'),
            tile('hospitality', 'Hospitality'),
            tile('it-gyan', 'IT Gyan'),
          ],
        },
        {
          slug: 'blue-collar',
          title: 'Blue Collar Knook',
          icon: `${A}knook-blue-collar.png`,
          expanded: false,
          modules: [
            tile('surgical-strike', 'Electrician Basics'),
            tile('sharp-shooter', 'Safety First'),
            tile('it-gyan', 'Plumbing 101'),
            tile('banking', 'Retail Floor'),
            tile('hospitality', 'Delivery Pro'),
            tile('insurance', 'Security Guard'),
          ].map((m, i) => ({
            ...m,
            slug: `blue-collar-${i + 1}`,
            link: `/learn/blue-collar-${i + 1}`,
          })),
        },
        {
          slug: 'practice',
          title: 'Practice Knook',
          icon: `${A}knook-practice.png`,
          expanded: false,
          modules: [
            {
              ...tile('sharp-shooter', 'Mock Interviews'),
              slug: 'mock-interviews',
              link: '/learn/mock-interviews',
            },
          ],
        },
        {
          slug: 'course',
          title: 'Course Knook',
          icon: `${A}knook-course.png`,
          expanded: false,
          modules: [
            {
              ...tile('it-gyan', 'Digital Basics'),
              slug: 'digital-basics',
              link: '/learn/digital-basics',
            },
          ],
        },
      ],
      rewardsReady: 2,
      unreadNotifications: 1,
    });
  }),
];
```

## `src/mocks/identity.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { isNewLearner, learnerName } from './persona';

/**
 * DigiLocker link (A-272). The mock returns what a real DigiLocker pull gives Skillmuni after
 * masking: never the full number. Kept in sessionStorage like the other mock stores.
 */
const KEY = 'sm_mock_identity';

const MASKS: Record<string, { masked: string; last4: string }> = {
  aadhaar: { masked: 'XXXX XXXX 4821', last4: '4821' },
  pan: { masked: 'XXXXXX234F', last4: '234F' },
  'driving-licence': { masked: 'XXXX XXXXXXX7731', last4: '7731' },
};

function read(): Record<string, string> | null {
  try {
    return JSON.parse(sessionStorage.getItem(KEY) ?? 'null') as Record<string, string> | null;
  } catch {
    return null;
  }
}

export const identityHandlers = [
  http.get('/api/me/identity', async () => {
    await delay(150);
    return HttpResponse.json(read());
  }),

  http.post('/api/me/identity/digilocker/start', async () => {
    await delay(150);
    return HttpResponse.json({ authorizeUrl: '/quests/who-dis?part=2&digilocker=sandbox' });
  }),

  http.post('/api/me/identity/digilocker/link', async ({ request }) => {
    await delay(600);
    const { doc, code } = (await request.json()) as { doc: string; code: string };
    const mask = MASKS[doc];
    if (!mask || !code) return new HttpResponse(null, { status: 400 });
    const record = {
      doc,
      ...mask,
      name: learnerName().full,
      birthday: isNewLearner() ? '2004-08-21' : '2003-05-14',
      verifiedAt: new Date().toISOString(),
    };
    try {
      sessionStorage.setItem(KEY, JSON.stringify(record));
    } catch {
      /* private mode */
    }
    return HttpResponse.json(record);
  }),

  http.delete('/api/me/identity', async () => {
    await delay(150);
    try {
      sessionStorage.removeItem(KEY);
    } catch {
      /* private mode */
    }
    return new HttpResponse(null, { status: 204 });
  }),
];
```

## `src/mocks/job-profile.handlers.ts`

```ts
import { profileProgress } from './profile-progress';
import { HttpResponse, delay, http } from 'msw';

/**
 * My Job Profile v2 (Figma 308:43436). Sample record from the design. In the real app the server
 * builds this from the 8 registration quests (one record, no second form).
 * Page URL scenarios (`?jp=`): fresher (P7) · finish (P6, sections to finish) · error.
 * Accepted CV changes (P5) and a rewritten "About me" live in sessionStorage.
 */

const KEY = 'sm_mock_job_profile';

interface State {
  about: string | null;
  added: string[];
  hideContact: boolean;
  expiresDays: number;
}

function scenario(): string {
  return typeof location === 'undefined'
    ? ''
    : (new URLSearchParams(location.search).get('jp') ?? '');
}

function load(): State {
  const base: State = { about: null, added: [], hideContact: true, expiresDays: 30 };
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? { ...base, ...(JSON.parse(raw) as Partial<State>) } : base;
  } catch {
    return base;
  }
}

function save(s: State): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    /* private mode */
  }
}

const MUNI_ABOUT =
  'Customer-first B.Com graduate with 6 months of voice support at Teleperformance and a year of retail sales. Calm on tough calls, quick on Excel, fluent in English, Hindi and Marathi. Looking for a full-time customer support or BPO voice role in Pune.';

const SOURCES = {
  about: { slug: 'who-dis', part: 1, name: 'Who Dis?' },
  contact: { slug: 'hit-me-up', part: 1, name: 'Hit Me Up' },
  work: { slug: 'work-lore', part: 1, name: 'Work Lore' },
  education: { slug: 'big-brain-stuff', part: 1, name: 'Big Brain Stuff' },
  languages: { slug: 'your-flex-era', part: 2, name: 'Your Flex Era' },
  skills: { slug: 'your-flex-era', part: 1, name: 'Your Flex Era' },
  certificates: { slug: 'drop-the-receipts', part: 1, name: 'Drop the Receipts' },
  lookingFor: { slug: 'pick-your-arc', part: 1, name: 'Pick Your Arc' },
};

const CHANGES = [
  {
    id: 'job-concentrix',
    kind: 'job',
    title: 'Customer Support Executive · Concentrix',
    detail: 'New job · Jul 2025 – now',
    suggest: 'add',
  },
  { id: 'skill-freshdesk', kind: 'skill', title: 'Freshdesk', detail: 'New skill', suggest: 'add' },
  {
    id: 'cert-udemy',
    kind: 'certificate',
    title: 'Customer Service Excellence · Udemy',
    detail: 'New certificate · needs proof to verify',
    suggest: 'add',
  },
  {
    id: 'email-outlook',
    kind: 'contact',
    title: 'Email: yash.p@outlook.com',
    detail: 'Different from your verified email',
    suggest: 'keep',
  },
];

function build(s: State) {
  const sc = scenario();
  const fresher = sc === 'fresher';
  const has = (id: string) => s.added.includes(id);
  const work = fresher
    ? []
    : [
        ...(has('job-concentrix')
          ? [
              {
                id: 'concentrix',
                initials: 'CX',
                role: 'Customer Support Executive',
                employer: 'Concentrix',
                city: 'Pune',
                period: 'Jul 2025 – now',
                bullets: [],
                proof: 'none',
              },
            ]
          : []),
        {
          id: 'tp',
          initials: 'TP',
          role: 'Customer Service Intern',
          employer: 'Teleperformance',
          city: 'Pune',
          period: 'Jan 2025 – Jun 2025 · 6 mos',
          bullets: ['Handled 60+ inbound calls a day for a telecom client · 92% CSAT'],
          proof: 'none',
        },
        {
          id: 'rt',
          initials: 'RT',
          role: 'Sales Associate (part-time)',
          employer: 'Reliance Trends',
          city: 'Pune',
          period: 'Mar 2023 – Apr 2024 · 1 yr 2 mos',
          bullets: ['Top seller 3 months running · handled billing and stock entry'],
          proof: 'verified',
        },
      ];
  const skills = [
    { name: 'Customer handling', power: 3, signature: true },
    { name: 'MS Excel', power: 3, signature: true },
    { name: 'Communication', power: 3, signature: true },
    { name: 'Data entry', power: 3, signature: false },
    { name: 'Zoho CRM', power: 2, signature: false },
    { name: 'Sales calls', power: 2, signature: false },
    { name: 'Problem solving', power: 2, signature: false },
    { name: 'Hindi typing', power: 2, signature: false },
    ...(has('skill-freshdesk') ? [{ name: 'Freshdesk', power: 2, signature: false }] : []),
    { name: 'Tally basics', power: null, signature: false },
  ];
  const certificates = [
    {
      id: 'nsdc',
      title: 'Data capturing certificate',
      issuer: 'NSDC · 2024',
      status: 'verified',
      eta: null,
    },
    {
      id: 'google',
      title: 'Fundamentals of Digital Marketing',
      issuer: 'Google · 2025',
      status: 'checking',
      eta: '48 h',
    },
    ...(has('cert-udemy')
      ? [
          {
            id: 'udemy',
            title: 'Customer Service Excellence',
            issuer: 'Udemy · 2025',
            status: 'none',
            eta: null,
          },
        ]
      : []),
    ...(fresher
      ? []
      : [
          {
            id: 'tp-letter',
            title: 'Teleperformance internship letter',
            issuer: 'Work proof',
            status: 'none',
            eta: null,
          },
        ]),
  ];
  const quickWins = [
    {
      id: 'tally',
      title: 'Set your level for Tally basics',
      impact: '+3 job matches',
      link: '/quests/your-flex-era',
      query: { part: 1, edit: 1 },
    },
    ...(fresher
      ? [
          {
            id: 'project',
            title: 'Add a college project or volunteering',
            impact: 'Freshers with one get 2× more invites',
            link: '/quests/work-lore',
            query: { part: 1, edit: 1 },
          },
        ]
      : [
          {
            id: 'tp-proof',
            title: 'Add proof for your Teleperformance internship',
            impact: 'Gets a ✓ Verified badge',
            link: '/quests/drop-the-receipts',
            query: { part: 1, edit: 1 },
          },
        ]),
  ];
  const progress = profileProgress();
  return {
    byQuest: progress.quests,
    name: 'Yash Pohare',
    initials: 'Y',
    headline: 'Customer Support · BPO Voice',
    archetype: { name: 'People Pro', strongest: 'People' },
    radar: { people: 92, business: 74, data: 52, creative: 38, tech: 46 },
    signature: [
      { name: 'Customer handling', power: 3 },
      { name: 'MS Excel', power: 2 },
      { name: 'Communication', power: 2 },
    ],
    counts: { skills: skills.length, languages: 3, trophies: 2 },
    strength: {
      percent: progress.percent,
      label: fresher ? 'Good start' : 'Strong profile',
      jobMatches: fresher ? 8 : 12,
      quickWins,
    },
    contact: {
      mobile: '+91 98••• ••210',
      mobileVerified: true,
      email: 'yash••••@gmail.com',
      location: 'Kothrud, Pune 411038',
      city: 'Pune',
      discoverable: true,
      invitesBy: 'Email · WhatsApp',
      neverShown: 'ID number, full address',
    },
    about: {
      text:
        s.about ??
        (fresher
          ? 'B.Com graduate from Savitribai Phule Pune University, quick on Excel and fluent in English, Hindi and Marathi. Looking for my first full-time customer support or BPO voice role in Pune.'
          : MUNI_ABOUT),
      byMuni: s.about === null,
      tags: fresher
        ? ['People Pro', 'Fresher', 'Immediate joiner']
        : ['People Pro', '2 yrs 2 mos experience', 'Immediate joiner'],
    },
    work,
    education: [
      {
        id: 'bcom',
        initials: 'SP',
        title: 'B.Com',
        school: 'Savitribai Phule Pune University',
        detail: '2021 – 2024 · CGPA 7.4',
        proof: 'verified',
      },
      {
        id: 'hsc',
        initials: '12',
        title: '12th · Commerce (HSC)',
        school: 'Maharashtra State Board',
        detail: '2021 · 78%',
        proof: 'verified',
      },
    ],
    languages: [
      { name: 'English', skills: 'Speak · Read · Write', level: 'Fluent' },
      { name: 'Hindi', skills: 'Speak · Read · Write', level: 'Native' },
      { name: 'Marathi', skills: 'Speak · Read', level: 'Native' },
    ],
    skills,
    certificates,
    lookingFor: [
      { label: 'Looking for', value: 'Full-time · Internship' },
      { label: 'Roles', value: 'Customer support · BPO voice · Data entry' },
      { label: 'Where', value: 'Pune + 25 km · open to remote' },
      { label: 'Shifts', value: 'Day · Rotational' },
      { label: 'Can join', value: 'Immediately', good: true },
      { label: 'Expected pay', value: '₹3 – 4.5 LPA', private: true },
    ],
    verifyCode: 'YP7Q4',
    verifiedCount: fresher ? 3 : 4,
    updatedAt: new Date().toISOString(),
    pending:
      sc === 'finish'
        ? [
            {
              slug: 'your-flex-era',
              title: 'Your Flex Era · skills',
              detail: 'About 3 min · pre-filled from your CV',
              xp: 20,
            },
            { slug: 'your-flex-era', title: 'Language passport', detail: 'About 1 min', xp: 20 },
          ]
        : [],
    sources: SOURCES,
  };
}

export const jobProfileHandlers = [
  http.get('/api/job-profile', async () => {
    await delay(450);
    if (scenario() === 'error') return new HttpResponse(null, { status: 503 });
    return HttpResponse.json(build(load()));
  }),
  http.get('/api/job-profile/cv-contact', async () => {
    await delay(250);
    return HttpResponse.json({
      phone: '+91 98765 43210',
      email: 'yash.pohare@gmail.com',
      location: 'Kothrud, Pune',
      linkedin: 'linkedin.com/in/yashpohare',
      expectedPay: '₹3 – 4.5 LPA',
    });
  }),
  http.put('/api/job-profile/about', async ({ request }) => {
    await delay(400);
    const { text } = (await request.json()) as { text: string };
    const s = load();
    s.about = text.trim() || null;
    save(s);
    return HttpResponse.json(build(s));
  }),
  http.post('/api/job-profile/cv', async () => {
    await delay(1200);
    const s = load();
    return HttpResponse.json(CHANGES.filter((c) => !s.added.includes(c.id)));
  }),
  http.post('/api/job-profile/cv/apply', async ({ request }) => {
    await delay(500);
    const { ids } = (await request.json()) as { ids: string[] };
    const s = load();
    s.added = [...new Set([...s.added, ...ids.filter((id) => id !== 'email-outlook')])];
    save(s);
    return HttpResponse.json(build(s));
  }),
  http.get('/api/job-profile/share', async () => {
    await delay(250);
    const s = load();
    return HttpResponse.json({
      url: 'https://skillmuni.app/cv/yash-7q4',
      hideContact: s.hideContact,
      expiresDays: s.expiresDays,
      viewsThisWeek: 3,
    });
  }),
  http.put('/api/job-profile/share', async ({ request }) => {
    await delay(250);
    const body = (await request.json()) as { hideContact: boolean; expiresDays: number };
    const s = { ...load(), ...body };
    save(s);
    return HttpResponse.json({
      url: 'https://skillmuni.app/cv/yash-7q4',
      hideContact: s.hideContact,
      expiresDays: s.expiresDays,
      viewsThisWeek: 3,
    });
  }),
];
```

## `src/mocks/jobs.data.ts`

```ts
/**
 * My Jobs v2 mock catalogue (Figma 272:22353). The first six jobs are the ones drawn in Figma;
 * the rest are generated deterministically so filters have real counts (128 open jobs).
 */
import type { Job, JobType, MatchInfo, WorkMode } from '../app/core/jobs/jobs.api';

export const SECTORS: Record<string, readonly string[]> = {
  'BPO / Contact centre': ['Customer service', 'Technical support', 'Collections', 'Chat support'],
  Insurance: ['Customer service', 'Claims processing', 'Sales'],
  'IT & Tech support': ['Technical support', 'Desktop support', 'QA tester'],
  Retail: ['Store associate', 'Cashier', 'Visual merchandiser'],
  Hospitality: ['Front desk', 'Guest relations', 'F&B service'],
  'Banking & Finance': ['Relationship officer', 'Back office', 'Sales'],
  'E-commerce & Logistics': ['Delivery associate', 'Warehouse associate', 'Customer service'],
  Education: ['Teaching assistant', 'Counsellor', 'Admissions'],
  Healthcare: ['Patient care', 'Front desk', 'Pharmacy assistant'],
  Telecom: ['Sales', 'Customer service', 'Field technician'],
  Manufacturing: ['Machine operator', 'Quality inspector', 'Store keeper'],
  'Media & Marketing': ['Content writer', 'Social media assistant', 'Graphic designer'],
  'Real estate': ['Sales', 'Site coordinator'],
  'Data & Admin': ['Data entry', 'Office assistant', 'Back office'],
};

export const STATES: Record<string, readonly string[]> = {
  Maharashtra: ['Pune', 'Mumbai', 'Nagpur', 'Nashik'],
  Karnataka: ['Bengaluru', 'Mysuru'],
  Telangana: ['Hyderabad'],
  'Tamil Nadu': ['Chennai'],
  Delhi: ['New Delhi'],
  Goa: ['Goa'],
};

const COMPANIES: readonly [string, string, string][] = [
  ['Teleperformance', 'TP', 'BPO / Contact centre'],
  ['HDFC Life', 'HL', 'Insurance'],
  ['Concentrix', 'CX', 'BPO / Contact centre'],
  ['Reliance Trends', 'RT', 'Retail'],
  ['Tata AIG', 'TA', 'Insurance'],
  ['Taj Hotels', 'TH', 'Hospitality'],
  ['ICICI Lombard', 'IL', 'Insurance'],
  ['Infosys BPM', 'IB', 'IT & Tech support'],
  ['Amazon', 'AZ', 'E-commerce & Logistics'],
  ['Axis Bank', 'AX', 'Banking & Finance'],
  ['Apollo Hospitals', 'AH', 'Healthcare'],
  ['Airtel', 'AT', 'Telecom'],
  ['Tata Motors', 'TM', 'Manufacturing'],
  ['upGrad', 'UG', 'Education'],
  ['Zomato', 'ZO', 'E-commerce & Logistics'],
  ['Wipro', 'WI', 'IT & Tech support'],
  ['Lodha Group', 'LG', 'Real estate'],
  ['Ogilvy', 'OG', 'Media & Marketing'],
  ['Quess Corp', 'QC', 'Data & Admin'],
];

const TITLES: Record<string, string> = {
  'Customer service': 'Customer Service Associate',
  'Technical support': 'Technical Support Associate',
  Collections: 'Collections Executive',
  'Chat support': 'Chat Support Executive',
  'Claims processing': 'Claims Processing Associate',
  Sales: 'Sales Executive',
  'Desktop support': 'Desktop Support Engineer',
  'QA tester': 'Junior QA Tester',
  'Store associate': 'Retail Store Associate',
  Cashier: 'Cashier',
  'Visual merchandiser': 'Visual Merchandiser',
  'Front desk': 'Front Desk Associate',
  'Guest relations': 'Guest Relations Executive',
  'F&B service': 'F&B Service Associate',
  'Relationship officer': 'Relationship Officer',
  'Back office': 'Back Office Executive',
  'Delivery associate': 'Delivery Associate',
  'Warehouse associate': 'Warehouse Associate',
  'Teaching assistant': 'Teaching Assistant',
  Counsellor: 'Career Counsellor',
  Admissions: 'Admissions Executive',
  'Patient care': 'Patient Care Assistant',
  'Pharmacy assistant': 'Pharmacy Assistant',
  'Field technician': 'Field Technician',
  'Machine operator': 'Machine Operator',
  'Quality inspector': 'Quality Inspector',
  'Store keeper': 'Store Keeper',
  'Content writer': 'Junior Content Writer',
  'Social media assistant': 'Social Media Assistant',
  'Graphic designer': 'Junior Graphic Designer',
  'Site coordinator': 'Site Coordinator',
  'Data entry': 'Data Entry Operator',
  'Office assistant': 'Office Assistant',
};

const MODES: readonly WorkMode[] = ['onsite', 'onsite', 'onsite', 'hybrid', 'remote'];
const TYPES: readonly JobType[] = [
  'full-time',
  'full-time',
  'full-time',
  'learnership',
  'part-time',
  'contract',
];

const ABOUT_CS =
  'Be the first voice customers hear. You’ll help customers of a leading telecom brand over calls and chat, solve their questions and make sure they leave happy. Full training is provided in your first 4 weeks.';

/** Small deterministic PRNG so the catalogue is the same on every load. */
function rng(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s * 1664525 + 1013904223) % 4294967296;
    return s / 4294967296;
  };
}

export function payLabel(type: JobType, min: number, max: number): string {
  if (type === 'learnership') return `₹${(min * 1000).toLocaleString('en-IN')} / month stipend`;
  return min === max ? `₹${min.toFixed(1)} LPA` : `₹${min.toFixed(1)}–${max.toFixed(1)} LPA`;
}

/** Annual pay in lakh for the pay filter; stipends count as monthly × 12. */
export const payIndex = new Map<string, number>();

function match(percent: number, skills: string[], city: string, gap: boolean): MatchInfo {
  return {
    percent,
    skills,
    reasons: [
      {
        ok: true,
        text: `Skills: ${Math.min(5, skills.length + 1)} of 5 match (${skills.map((s) => s.toLowerCase()).join(', ')})`,
      },
      {
        ok: true,
        text:
          city === 'Pune'
            ? 'Location: Pune, where you live'
            : `Location: ${city}, you said you’d relocate`,
      },
      { ok: true, text: 'Your assessment: People-First Service Builder fits people-facing roles' },
      ...(gap ? [{ ok: false, text: 'Missing: basic MS Excel' }] : []),
    ],
    gap: gap ? { label: 'Close the gap: Excel basics · 20 min course', link: '/learn' } : null,
  };
}

interface Seed {
  title: string;
  company: string;
  initials: string;
  city: string;
  state: string;
  sector: string;
  role: string;
  workMode: WorkMode;
  jobType: JobType;
  months?: number;
  pay: [number, number];
  postedDays: number;
  closingInDays?: number;
  saved?: boolean;
  match?: MatchInfo;
}

function make(id: string, s: Seed, r: () => number): Job {
  const remote = s.workMode === 'remote';
  payIndex.set(id, s.jobType === 'learnership' ? (s.pay[0] * 12) / 100 : s.pay[0]);
  const typeLabel =
    s.jobType === 'contract'
      ? `Contract · ${s.months ?? 6} months`
      : s.jobType === 'learnership'
        ? 'Learnership'
        : s.jobType === 'part-time'
          ? 'Part-time'
          : 'Full-time';
  return {
    id,
    title: s.title,
    company: s.company,
    initials: s.initials,
    city: remote ? 'Remote' : s.city,
    state: remote ? '' : s.state,
    place: remote ? 'Remote · India' : s.city === s.state ? s.city : `${s.city}, ${s.state}`,
    sector: s.sector,
    role: s.role,
    workMode: s.workMode,
    jobType: s.jobType,
    jobTypeLabel: typeLabel,
    pay: payLabel(s.jobType, s.pay[0], s.pay[1]),
    postedDays: s.postedDays,
    applicants: 12 + Math.floor(r() * 80),
    applyBy: '15 Oct',
    closingInDays: s.closingInDays ?? null,
    closed: false,
    isNew: s.postedDays <= 2,
    saved: s.saved ?? false,
    savedDays: s.saved ? 2 : null,
    appliedOn: null,
    experience: '0–1 years (freshers welcome)',
    openings: 2 + Math.floor(r() * 14),
    shift: s.sector === 'BPO / Contact centre' ? 'Rotational · 5 days/week' : 'Day · 6 days/week',
    about:
      s.role === 'Customer service'
        ? ABOUT_CS
        : `Join ${s.company} as ${/^[AEIOU]/.test(s.title) ? 'an' : 'a'} ${s.title}. You’ll learn on the job with a buddy for your first month and grow into a full role.`,
    duties:
      s.role === 'Customer service'
        ? [
            'Handle 40–60 customer calls and chats a day',
            'Log every interaction in the CRM',
            'Escalate complex issues to the right team',
          ]
        : [
            'Learn the team’s tools in your first two weeks',
            'Own a daily set of tasks and targets',
            'Share updates with your team lead',
          ],
    requirements: [
      '12th pass or graduate',
      'Good spoken English and Hindi',
      ...(s.sector === 'BPO / Contact centre' ? ['Comfortable with rotational shifts'] : []),
      'Basic computer skills (MS Office)',
    ],
    skills:
      s.role === 'Customer service'
        ? ['Communication', 'Customer service', 'English', 'Hindi', 'CRM', 'MS Office']
        : ['Communication', 'Teamwork', 'MS Office'],
    companyAbout: `${s.company} is a leading employer in ${s.sector.toLowerCase()} with teams across India. Benefits: health insurance, performance bonus${
      s.sector === 'BPO / Contact centre' ? ', cab facility for night shifts' : ''
    }.`,
    match: s.match ?? null,
  };
}

function build(): Job[] {
  const r = rng(42);
  const fixed: Seed[] = [
    {
      title: 'Customer Service Associate',
      company: 'Teleperformance',
      initials: 'TP',
      city: 'Pune',
      state: 'Maharashtra',
      sector: 'BPO / Contact centre',
      role: 'Customer service',
      workMode: 'onsite',
      jobType: 'full-time',
      pay: [2.4, 3.0],
      postedDays: 2,
      match: match(92, ['Communication', 'English', 'Customer service'], 'Pune', true),
    },
    {
      title: 'Inbound Sales Executive',
      company: 'HDFC Life',
      initials: 'HL',
      city: 'Mumbai',
      state: 'Maharashtra',
      sector: 'Insurance',
      role: 'Sales',
      workMode: 'hybrid',
      jobType: 'full-time',
      pay: [3.0, 4.0],
      postedDays: 5,
      closingInDays: 3,
      saved: true,
      match: match(84, ['Communication', 'Sales', 'Hindi'], 'Mumbai', true),
    },
    {
      title: 'Technical Support Trainee',
      company: 'Concentrix',
      initials: 'CX',
      city: 'Bengaluru',
      state: 'Karnataka',
      sector: 'BPO / Contact centre',
      role: 'Technical support',
      workMode: 'onsite',
      jobType: 'learnership',
      pay: [15, 15],
      postedDays: 1,
      saved: true,
      match: match(78, ['Tech support', 'English'], 'Bengaluru', false),
    },
    {
      title: 'Retail Store Associate',
      company: 'Reliance Trends',
      initials: 'RT',
      city: 'Pune',
      state: 'Maharashtra',
      sector: 'Retail',
      role: 'Store associate',
      workMode: 'onsite',
      jobType: 'part-time',
      pay: [1.8, 1.8],
      postedDays: 7,
      match: match(66, ['Communication', 'Retail'], 'Pune', false),
    },
    {
      title: 'Data Entry Operator',
      company: 'Tata AIG',
      initials: 'TA',
      city: 'Remote',
      state: '',
      sector: 'Data & Admin',
      role: 'Data entry',
      workMode: 'remote',
      jobType: 'contract',
      months: 6,
      pay: [2.2, 2.2],
      postedDays: 3,
      match: match(61, ['Typing', 'MS Office'], 'Remote', false),
    },
    {
      title: 'Front Desk Associate',
      company: 'Taj Hotels',
      initials: 'TH',
      city: 'Goa',
      state: 'Goa',
      sector: 'Hospitality',
      role: 'Front desk',
      workMode: 'onsite',
      jobType: 'full-time',
      pay: [2.6, 2.6],
      postedDays: 14,
      saved: true,
    },
    {
      title: 'Claims Processing Associate',
      company: 'ICICI Lombard',
      initials: 'IL',
      city: 'Pune',
      state: 'Maharashtra',
      sector: 'Insurance',
      role: 'Claims processing',
      workMode: 'onsite',
      jobType: 'full-time',
      pay: [2.8, 2.8],
      postedDays: 4,
    },
  ];
  const out = fixed.map((s, i) => make(`job-${i + 1}`, s, r));
  const stateNames = Object.keys(STATES);
  // Most generated jobs sit in Maharashtra so the Pune defaults have depth.
  for (let i = out.length; i < 128; i++) {
    const [company, initials, home] = COMPANIES[Math.floor(r() * COMPANIES.length)] ?? [
      'Teleperformance',
      'TP',
      'BPO / Contact centre',
    ];
    const sector = r() < 0.7 ? home : (Object.keys(SECTORS)[Math.floor(r() * 14)] ?? home);
    const roles = SECTORS[sector] ?? ['Customer service'];
    const role = roles[Math.floor(r() * roles.length)] ?? 'Customer service';
    const state =
      r() < 0.6
        ? 'Maharashtra'
        : (stateNames[Math.floor(r() * stateNames.length)] ?? 'Maharashtra');
    const cities = STATES[state] ?? ['Pune'];
    const city =
      state === 'Maharashtra' && r() < 0.55
        ? 'Pune'
        : (cities[Math.floor(r() * cities.length)] ?? 'Pune');
    const workMode = MODES[Math.floor(r() * MODES.length)] ?? 'onsite';
    const jobType = TYPES[Math.floor(r() * TYPES.length)] ?? 'full-time';
    const base = 1.6 + Math.round(r() * 30) / 10;
    const pay: [number, number] =
      jobType === 'learnership'
        ? [10 + Math.floor(r() * 8), 0]
        : [base, base + Math.round(r() * 10) / 10];
    if (jobType === 'learnership') pay[1] = pay[0];
    out.push(
      make(
        `job-${i + 1}`,
        {
          title: TITLES[role] ?? role,
          company,
          initials,
          city,
          state,
          sector,
          role,
          workMode,
          jobType,
          months: 6,
          pay,
          postedDays: 1 + Math.floor(r() * 40),
          closingInDays: r() < 0.08 ? 2 + Math.floor(r() * 4) : undefined,
          // A few people-facing roles match the sample profile too (Matching Jobs has 12).
          match:
            out.length < 60 &&
            ['Customer service', 'Sales', 'Chat support', 'Store associate'].includes(role)
              ? match(60 + Math.floor(r() * 18), ['Communication', 'English'], city, r() < 0.5)
              : undefined,
        },
        r,
      ),
    );
  }
  return out;
}

export const JOBS: readonly Job[] = build();
```

## `src/mocks/jobs.handlers.ts`

```ts
import { profileProgress, questsWithGaps } from './profile-progress';
import { HttpResponse, delay, http } from 'msw';
import { isNewLearner } from './persona';
import type {
  AlertSettings,
  Application,
  ApplicationStatus,
  Facet,
  FilterState,
  Invitation,
  Job,
  ListResult,
  Suggestion,
  TimelineStep,
} from '../app/core/jobs/jobs.api';
import { JOBS, SECTORS, STATES, payIndex } from './jobs.data';

/**
 * My Jobs v2 (Figma 272:22353). Faceted search over a 128-job catalogue: every option carries the
 * count it would give with the other filters applied, Role depends on Sector, City on State, and
 * zero results suggest which filter to drop (J6). Page URL scenarios (`?jobs=`): locked (J8) ·
 * empty (J16–J19) · error (J21) · slow (J20). Saves, applications and replies live in sessionStorage.
 */

const KEY = 'sm_mock_jobs';
/** Mock "today" so relative dates match the Figma frames. */
const TODAY = '2 Oct';

interface State {
  saved: string[];
  unsaved: string[];
  applied: Record<string, Application>;
  invites: Record<string, Invitation['status']>;
  withdrawn: string[];
  alerts: AlertSettings;
}

function scenario(): string {
  return typeof location === 'undefined'
    ? ''
    : (new URLSearchParams(location.search).get('jobs') ?? '');
}

function load(): State {
  const base: State = {
    saved: [],
    unsaved: [],
    applied: {},
    invites: {},
    withdrawn: [],
    alerts: { on: true, frequency: 'daily', searches: ['Customer service · Pune'] },
  };
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? { ...base, ...(JSON.parse(raw) as Partial<State>) } : base;
  } catch {
    return base;
  }
}

/** What the launchpad needs to know about My Jobs (A-306). */
export function jobsActivity(): { applied: number; saved: number; seen: boolean } {
  const s = load();
  const all = JOBS.map((j) => withState(j, s));
  let seen = false;
  try {
    seen = sessionStorage.getItem(SEEN_KEY) === '1';
  } catch {
    /* private mode */
  }
  return {
    applied: apps(s).filter((a) => a.status !== 'withdrawn').length,
    saved: all.filter((j) => j.saved).length,
    seen,
  };
}

/** Visiting My Jobs clears the "new matches" count on the launchpad. */
export function markJobsSeen(): void {
  try {
    sessionStorage.setItem(SEEN_KEY, '1');
  } catch {
    /* private mode */
  }
}

const SEEN_KEY = 'sm_mock_jobs_seen';

function save(s: State): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    /* private mode */
  }
}

function withState(j: Job, s: State): Job {
  const saved = (j.saved && !isNewLearner() && !s.unsaved.includes(j.id)) || s.saved.includes(j.id);
  const app =
    Object.values(s.applied).find((a) => a.job.id === j.id) ??
    seededApps().find((a) => a.job.id === j.id);
  return {
    ...j,
    saved,
    savedDays: saved ? (j.savedDays ?? 0) : null,
    appliedOn: app && app.status !== 'withdrawn' ? app.appliedOn : null,
  };
}

const byId = (id: string) => JOBS.find((j) => j.id === id);

/* ---------- Invitations ---------- */

function seededInvites(): Invitation[] {
  if (isNewLearner()) return [];
  const j = (id: string) => byId(id) as Job;
  return [
    {
      id: 'inv-1',
      job: j('job-1'),
      recruiter: { name: 'Priya S.', initials: 'PS', title: 'Recruiter, Teleperformance' },
      type: 'apply',
      interviewAt: null,
      message:
        'Hi Yash, your profile is a great fit for our Customer Service team in Pune. We’d love you to apply before 8 Oct.',
      respondBy: '8 Oct',
      daysLeft: 6,
      status: 'new',
    },
    {
      id: 'inv-2',
      job: j('job-2'),
      recruiter: { name: 'Rahul K.', initials: 'RK', title: 'Recruiter, HDFC Life' },
      type: 'interview',
      interviewAt: '2026-10-10T11:00:00+05:30',
      message:
        'Hi Yash, thanks for your interest in sales. We’d like to meet you for a 30-minute video interview on 10 Oct at 11:00 AM. If that doesn’t work, suggest another time.',
      respondBy: '6 Oct',
      daysLeft: 4,
      status: 'new',
    },
    {
      id: 'inv-3',
      job: j('job-3'),
      recruiter: { name: 'Anita M.', initials: 'AM', title: 'Talent partner, Concentrix' },
      type: 'apply',
      interviewAt: null,
      message:
        'Hi Yash, our Technical Support learnership starts in November. You’d be a good fit.',
      respondBy: '12 Oct',
      daysLeft: 10,
      status: 'accepted',
    },
    {
      id: 'inv-4',
      job: j('job-6'),
      recruiter: { name: 'Kavya R.', initials: 'KR', title: 'HR, Taj Hotels' },
      type: 'apply',
      interviewAt: null,
      message: 'Hi Yash, we have front desk openings in Goa this season.',
      respondBy: '28 Sep',
      daysLeft: 0,
      status: 'expired',
    },
  ];
}

/* ---------- Applications ---------- */

const STEPS = ['Applied', 'Viewed by recruiter', 'Shortlisted', 'Interview', 'Offer'];

function timeline(status: ApplicationStatus, dates: string[]): TimelineStep[] {
  if (status === 'withdrawn')
    return [
      { label: 'Applied', date: dates[0] ?? null, state: 'done' },
      { label: 'Withdrawn', date: dates[1] ?? null, state: 'failed' },
    ];
  if (status === 'not-selected')
    return [
      { label: 'Applied', date: dates[0] ?? null, state: 'done' },
      { label: 'Viewed by recruiter', date: dates[1] ?? null, state: 'done' },
      { label: 'Not selected', date: dates[2] ?? null, state: 'failed' },
    ];
  const at = { applied: 0, viewed: 1, shortlisted: 2, interview: 3, offer: 4 }[status];
  return STEPS.map((label, i) => ({
    label,
    date: dates[i] ?? null,
    state: i < at ? 'done' : i === at ? (i === 3 ? 'current' : 'done') : 'todo',
  }));
}

function seededApps(): Application[] {
  if (isNewLearner()) return [];
  const j = (id: string) => byId(id) as Job;
  const base = { cv: 'Yash_Pohare_CV_2026.pdf', answers: [], similar: 6 };
  return [
    {
      ...base,
      id: 'app-1',
      job: j('job-1'),
      appliedOn: '24 Sep',
      updated: 'Updated today',
      status: 'interview',
      timeline: timeline('interview', ['24 Sep', '26 Sep', '28 Sep', '12 Oct · 11:00 AM']),
      interview: {
        at: '2026-10-12T11:00:00+05:30',
        mode: 'Video interview · 30 min',
        detail: 'Sat, 12 Oct · 11:00 AM IST · Google Meet link arrives 1 day before',
      },
      answers: [
        { q: 'Are you comfortable with rotational shifts, including nights?', a: 'Yes' },
        { q: 'When can you join?', a: 'Immediately' },
      ],
    },
    {
      ...base,
      id: 'app-2',
      job: j('job-2'),
      appliedOn: '26 Sep',
      updated: 'Updated 2 days ago',
      status: 'shortlisted',
      timeline: timeline('shortlisted', ['26 Sep', '27 Sep', '30 Sep']),
      interview: null,
    },
    {
      ...base,
      id: 'app-3',
      job: j('job-3'),
      appliedOn: '28 Sep',
      updated: 'Viewed by recruiter',
      status: 'viewed',
      timeline: timeline('viewed', ['28 Sep', '29 Sep']),
      interview: null,
    },
    {
      ...base,
      id: 'app-4',
      job: j('job-4'),
      appliedOn: '10 Sep',
      updated: 'Closed 20 Sep',
      status: 'not-selected',
      timeline: timeline('not-selected', ['10 Sep', '12 Sep', '20 Sep']),
      interview: null,
    },
    {
      ...base,
      id: 'app-5',
      job: j('job-5'),
      appliedOn: '2 Sep',
      updated: 'You withdrew on 8 Sep',
      status: 'withdrawn',
      timeline: timeline('withdrawn', ['2 Sep', '8 Sep']),
      interview: null,
    },
  ];
}

function apps(s: State): Application[] {
  const all = [...Object.values(s.applied).reverse(), ...seededApps()];
  return all.map((a) =>
    s.withdrawn.includes(a.id) && a.status !== 'withdrawn'
      ? {
          ...a,
          status: 'withdrawn' as const,
          updated: `You withdrew on ${TODAY}`,
          interview: null,
          timeline: timeline('withdrawn', [a.appliedOn, TODAY]),
        }
      : a,
  );
}

function invites(s: State): Invitation[] {
  return seededInvites().map((i) => ({ ...i, status: s.invites[i.id] ?? i.status }));
}

/* ---------- Faceted filtering ---------- */

const LABELS: Record<string, Record<string, string>> = {
  workMode: { onsite: 'On-site', hybrid: 'Hybrid', remote: 'Remote' },
  jobType: {
    'full-time': 'Full-time',
    learnership: 'Learnership / Internship',
    'part-time': 'Part-time',
    contract: 'Contract',
  },
  pay: { '1': '₹1 LPA+', '2': '₹2 LPA+', '3': '₹3 LPA+', '5': '₹5 LPA+' },
  posted: { '1': 'Past 24 hours', '7': 'Past week', '30': 'Past month' },
  match: { '90': '90%+ match', '80': '80%+ match', '70': '70%+ match', '60': '60%+ match' },
  jobStatus: { open: 'Open', closing: 'Closing soon', closed: 'Closed' },
  remote: { yes: 'Include remote' },
  status: {
    new: 'New',
    accepted: 'Accepted',
    declined: 'Declined',
    expired: 'Expired',
    applied: 'Applied',
    viewed: 'Under review',
    shortlisted: 'Shortlisted',
    interview: 'Interview',
    offer: 'Offer',
    'not-selected': 'Not selected',
    withdrawn: 'Withdrawn',
  },
  inviteType: { apply: 'Invited to apply', interview: 'Interview invite' },
  dateApplied: { '7': 'Past week', '30': 'Past month', '90': 'Past 3 months' },
};

type Getter<T> = (item: T, key: string) => readonly string[] | null;

function jobValues(j: Job, key: string): readonly string[] | null {
  switch (key) {
    case 'sector':
      return [j.sector];
    case 'role':
      return [j.role];
    case 'state':
      return j.state ? [j.state] : [];
    case 'city':
      return j.workMode === 'remote' ? [] : [j.city];
    case 'workMode':
      return [j.workMode];
    case 'jobType':
      return [j.jobType];
    case 'pay':
      return ['1', '2', '3', '5'].filter((p) => (payIndex.get(j.id) ?? 0) >= Number(p));
    case 'posted':
      return ['1', '7', '30'].filter((d) => j.postedDays <= Number(d));
    case 'match':
      return ['60', '70', '80', '90'].filter((m) => (j.match?.percent ?? 0) >= Number(m));
    case 'jobStatus':
      return [j.closed ? 'closed' : j.closingInDays !== null ? 'closing' : 'open'];
    default:
      return null;
  }
}

/** City + "include remote" act together; everything else is AND across keys, OR within one. */
function passes<T>(item: T, f: FilterState, get: Getter<T>, skip?: string): boolean {
  for (const [key, want] of Object.entries(f)) {
    if (!want.length || key === skip || key === 'remote') continue;
    const have = get(item, key);
    if (have === null) continue;
    if (key === 'city' || key === 'state') {
      const remoteOk =
        f['remote']?.includes('yes') && (get(item, 'workMode') ?? []).includes('remote');
      if (remoteOk) continue;
    }
    if (!want.some((w) => have.includes(w))) return false;
  }
  return true;
}

function facet<T>(
  items: readonly T[],
  f: FilterState,
  get: Getter<T>,
  key: string,
  ids: readonly string[],
  label: (id: string) => string = (id) => LABELS[key]?.[id] ?? id,
): Facet {
  const pool = items.filter((i) => passes(i, f, get, key));
  return {
    key,
    options: ids.map((id) => ({
      id,
      label: label(id),
      count: pool.filter((i) => (get(i, key) ?? []).includes(id)).length,
    })),
  };
}

function suggestions<T>(items: readonly T[], f: FilterState, get: Getter<T>): Suggestion[] {
  const out: Suggestion[] = [];
  for (const [key, vals] of Object.entries(f)) {
    for (const id of vals) {
      const next = { ...f, [key]: vals.filter((v) => v !== id) };
      const count = items.filter((i) => passes(i, next, get)).length;
      if (count) out.push({ key, id, label: LABELS[key]?.[id] ?? id, count });
    }
  }
  return out.sort((a, b) => a.count - b.count).slice(0, 3);
}

function text<T>(items: readonly T[], q: string, place: string, job: (t: T) => Job): T[] {
  const ql = q.trim().toLowerCase();
  const pl = place.trim().toLowerCase();
  return items.filter((t) => {
    const j = job(t);
    const hay = `${j.title} ${j.company} ${j.skills.join(' ')} ${j.role} ${j.sector}`.toLowerCase();
    const where = `${j.city} ${j.state} ${j.workMode === 'remote' ? 'remote' : ''}`.toLowerCase();
    return (
      (!ql || hay.includes(ql)) &&
      (!pl || pl.split(/\s+and\s+|,\s*/).some((p) => where.includes(p.trim())))
    );
  });
}

function parse(url: URL): {
  q: string;
  place: string;
  sort: string;
  limit: number;
  filters: FilterState;
} {
  const sp = url.searchParams;
  let filters: FilterState = {};
  try {
    filters = JSON.parse(sp.get('filters') ?? '{}') as FilterState;
  } catch {
    /* ignore */
  }
  return {
    q: sp.get('q') ?? '',
    place: sp.get('place') ?? '',
    sort: sp.get('sort') ?? '',
    limit: sp.has('limit') ? Number(sp.get('limit')) : 20,
    filters,
  };
}

function jobFacets(items: readonly Job[], f: FilterState, tab: string): Facet[] {
  const sectors = Object.keys(SECTORS);
  const chosen = f['sector'] ?? [];
  const roles = [...new Set((chosen.length ? chosen : sectors).flatMap((s) => SECTORS[s] ?? []))];
  const state = f['state']?.[0];
  const cities = state ? (STATES[state] ?? []) : Object.values(STATES).flat();
  const remotePool = items.filter(
    (j) => passes(j, f, jobValues, 'city') && j.workMode === 'remote',
  ).length;
  const all: Facet[] = [
    facet(items, f, jobValues, 'sector', sectors),
    facet(items, f, jobValues, 'role', roles),
    facet(items, f, jobValues, 'state', Object.keys(STATES)),
    facet(items, f, jobValues, 'city', cities),
    { key: 'remote', options: [{ id: 'yes', label: 'Include remote jobs', count: remotePool }] },
    facet(items, f, jobValues, 'workMode', ['onsite', 'hybrid', 'remote']),
    facet(items, f, jobValues, 'jobType', ['full-time', 'learnership', 'part-time', 'contract']),
    facet(items, f, jobValues, 'pay', ['1', '2', '3', '5']),
    facet(items, f, jobValues, 'posted', ['1', '7', '30']),
  ];
  if (tab === 'matching')
    all.unshift(facet(items, f, jobValues, 'match', ['90', '80', '70', '60']));
  if (tab === 'bookmarked')
    all.unshift(facet(items, f, jobValues, 'jobStatus', ['open', 'closing', 'closed']));
  return all;
}

function sortJobs(items: Job[], sort: string): Job[] {
  const by: Record<string, (a: Job, b: Job) => number> = {
    relevant: (a, b) =>
      (b.match?.percent ?? 0) - (a.match?.percent ?? 0) ||
      Number(b.isNew) - Number(a.isNew) ||
      a.postedDays - b.postedDays,
    newest: (a, b) => a.postedDays - b.postedDays,
    salary: (a, b) => (payIndex.get(b.id) ?? 0) - (payIndex.get(a.id) ?? 0),
    match: (a, b) => (b.match?.percent ?? 0) - (a.match?.percent ?? 0),
    saved: (a, b) => (a.savedDays ?? 0) - (b.savedDays ?? 0),
    closing: (a, b) => (a.closingInDays ?? 99) - (b.closingInDays ?? 99),
  };
  return [...items].sort(by[sort] ?? by['relevant']);
}

function list<T>(
  items: T[],
  limit: number,
  facets: Facet[],
  sugg: Suggestion[],
  note: string | null,
): ListResult<T> {
  return {
    items: items.slice(0, limit),
    total: items.length,
    facets,
    suggestions: items.length ? [] : sugg,
    note,
  };
}

const fail = () => (scenario() === 'error' ? new HttpResponse(null, { status: 503 }) : null);
const wait = () => delay(scenario() === 'slow' ? 4000 : 350);

function summary(s: State) {
  const empty = scenario() === 'empty';
  const inv = invites(s);
  const all = JOBS.map((j) => withState(j, s));
  return {
    available: JOBS.length,
    matching: empty ? 0 : all.filter((j) => (j.match?.percent ?? 0) >= 60).length,
    invitations: empty ? 0 : inv.length,
    invitationsNew: empty ? 0 : inv.filter((i) => i.status === 'new').length,
    bookmarked: empty ? 0 : all.filter((j) => j.saved).length,
    applied: empty ? 0 : apps(s).length,
    profilePercent: scenario() === 'locked' ? 45 : profileProgress().percent,
    unlockAt: 0,
    missing: questsWithGaps(),
    minutesToUnlock: 5,
    defaultPlace: 'Pune and Remote',
  };
}

export const jobsHandlers = [
  http.get('/api/jobs/summary', async () => {
    await delay(200);
    markJobsSeen();
    return fail() ?? HttpResponse.json(summary(load()));
  }),

  http.get('/api/jobs/list/:tab', async ({ params, request }) => {
    await wait();
    const err = fail();
    if (err) return err;
    const tab = String(params['tab']);
    const s = load();
    const q = parse(new URL(request.url));
    let pool = JOBS.map((j) => withState(j, s));
    if (scenario() === 'empty' && tab !== 'available') pool = [];
    if (tab === 'matching') pool = pool.filter((j) => (j.match?.percent ?? 0) >= 60);
    if (tab === 'bookmarked') pool = pool.filter((j) => j.saved);
    // The place box is a shortcut for Location: once State/City is picked, the filter wins.
    const byLocation = !!(q.filters['state']?.length || q.filters['city']?.length);
    pool = text(pool, q.q, byLocation ? '' : q.place, (j) => j);
    const hits = sortJobs(
      pool.filter((j) => passes(j, q.filters, jobValues)),
      q.sort || (tab === 'matching' ? 'match' : tab === 'bookmarked' ? 'saved' : 'relevant'),
    );
    const closing = hits.filter((j) => j.closingInDays !== null).length;
    const note =
      tab === 'bookmarked'
        ? `${hits.length} saved job${hits.length === 1 ? '' : 's'}${closing ? ` · ${closing} closing soon` : ''}`
        : null;
    return HttpResponse.json(
      list(
        hits,
        q.limit,
        jobFacets(pool, q.filters, tab),
        suggestions(pool, q.filters, jobValues),
        note,
      ),
    );
  }),

  http.get('/api/jobs/invitations', async ({ request }) => {
    await wait();
    const err = fail();
    if (err) return err;
    const s = load();
    const q = parse(new URL(request.url));
    const get: Getter<Invitation> = (i, key) =>
      key === 'status'
        ? [i.status]
        : key === 'inviteType'
          ? [i.type]
          : key === 'company'
            ? [i.job.company]
            : null;
    let pool = scenario() === 'empty' ? [] : invites(s);
    pool = text(pool, q.q, '', (i) => i.job);
    const hits = pool
      .filter((i) => passes(i, q.filters, get))
      .sort((a, b) =>
        q.sort === 'newest'
          ? b.id.localeCompare(a.id)
          : (a.status === 'new' ? 0 : 1) - (b.status === 'new' ? 0 : 1) || a.daysLeft - b.daysLeft,
      );
    const companies = [...new Set(pool.map((i) => i.job.company))];
    const facets = [
      facet(pool, q.filters, get, 'status', ['new', 'accepted', 'declined', 'expired']),
      facet(pool, q.filters, get, 'inviteType', ['apply', 'interview']),
      facet(pool, q.filters, get, 'company', companies),
    ];
    const fresh = hits.filter((i) => i.status === 'new').length;
    return HttpResponse.json(
      list(
        hits,
        q.limit,
        facets,
        suggestions(pool, q.filters, get),
        `${fresh} new · ${hits.length} total`,
      ),
    );
  }),

  http.get('/api/jobs/applications', async ({ request }) => {
    await wait();
    const err = fail();
    if (err) return err;
    const s = load();
    const q = parse(new URL(request.url));
    const age: Record<string, number> = {
      '24 Sep': 8,
      '26 Sep': 6,
      '28 Sep': 4,
      '10 Sep': 22,
      '2 Sep': 30,
    };
    const get: Getter<Application> = (a, key) =>
      key === 'status'
        ? [a.status]
        : key === 'sector'
          ? [a.job.sector]
          : key === 'company'
            ? [a.job.company]
            : key === 'dateApplied'
              ? ['7', '30', '90'].filter((d) => (age[a.appliedOn] ?? 0) <= Number(d))
              : null;
    let pool = scenario() === 'empty' ? [] : apps(s);
    pool = text(pool, q.q, '', (a) => a.job);
    const hits = pool.filter((a) => passes(a, q.filters, get));
    const facets = [
      facet(pool, q.filters, get, 'status', [
        'applied',
        'viewed',
        'shortlisted',
        'interview',
        'offer',
        'not-selected',
        'withdrawn',
      ]),
      facet(pool, q.filters, get, 'dateApplied', ['7', '30', '90']),
      facet(pool, q.filters, get, 'sector', [...new Set(pool.map((a) => a.job.sector))]),
      facet(pool, q.filters, get, 'company', [...new Set(pool.map((a) => a.job.company))]),
    ];
    const upcoming = hits.filter((a) => a.status === 'interview').length;
    const note = `${hits.length} application${hits.length === 1 ? '' : 's'}${upcoming ? ` · ${upcoming} interview coming up` : ''}`;
    return HttpResponse.json(list(hits, q.limit, facets, suggestions(pool, q.filters, get), note));
  }),

  http.get('/api/jobs/job/:id', async ({ params }) => {
    await delay(200);
    const j = byId(String(params['id']));
    return j ? HttpResponse.json(withState(j, load())) : new HttpResponse(null, { status: 404 });
  }),

  http.get('/api/jobs/invitations/:id', async ({ params }) => {
    await delay(200);
    const i = invites(load()).find((x) => x.id === params['id']);
    return i ? HttpResponse.json(i) : new HttpResponse(null, { status: 404 });
  }),

  http.get('/api/jobs/applications/:id', async ({ params }) => {
    await delay(200);
    const a = apps(load()).find((x) => x.id === params['id']);
    return a ? HttpResponse.json(a) : new HttpResponse(null, { status: 404 });
  }),

  http.put('/api/jobs/job/:id/saved', async ({ params, request }) => {
    await delay(200);
    const id = String(params['id']);
    const { saved } = (await request.json()) as { saved: boolean };
    const s = load();
    s.saved = s.saved.filter((x) => x !== id);
    s.unsaved = s.unsaved.filter((x) => x !== id);
    (saved ? s.saved : s.unsaved).push(id);
    save(s);
    const j = byId(id);
    return j ? HttpResponse.json(withState(j, s)) : new HttpResponse(null, { status: 404 });
  }),

  http.get('/api/jobs/job/:id/apply', async () => {
    await delay(250);
    return HttpResponse.json({
      name: 'Yash Pohare',
      contact: '+91 98•••• 0697 · y••••@gmail.com · Pune, Maharashtra',
      cvs: [
        { id: 'cv-1', name: 'Yash_Pohare_CV_2026.pdf', meta: 'Updated 3 Sep · 212 KB' },
        { id: 'cv-2', name: 'Yash_CV_Sales.pdf', meta: 'Updated 12 Aug · 188 KB' },
      ],
      questions: [
        {
          id: 'q1',
          text: 'Are you comfortable with rotational shifts, including nights?',
          kind: 'yes-no',
        },
        { id: 'q2', text: 'When can you join?', kind: 'text' },
      ],
    });
  }),

  http.post('/api/jobs/cv', async ({ request }) => {
    await delay(700);
    const f = (await request.formData()).get('file');
    const name = f instanceof File ? f.name : 'My_CV.pdf';
    const kb = f instanceof File ? Math.max(1, Math.round(f.size / 1024)) : 120;
    return HttpResponse.json({ id: `cv-${Date.now()}`, name, meta: `Uploaded today · ${kb} KB` });
  }),

  http.post('/api/jobs/job/:id/apply', async ({ params, request }) => {
    await delay(700);
    const id = String(params['id']);
    const j = byId(id);
    if (!j) return new HttpResponse(null, { status: 404 });
    const body = (await request.json()) as {
      cvId: string;
      answers: Record<string, string>;
      invitationId?: string;
    };
    const s = load();
    const app: Application = {
      id: `app-new-${id}`,
      job: { ...j, appliedOn: TODAY },
      appliedOn: TODAY,
      updated: 'Updated today',
      cv: body.cvId === 'cv-2' ? 'Yash_CV_Sales.pdf' : 'Yash_Pohare_CV_2026.pdf',
      status: 'applied',
      timeline: timeline('applied', [TODAY]),
      interview: null,
      answers: Object.values(body.answers).map((a, i) => ({
        q:
          i === 0
            ? 'Are you comfortable with rotational shifts, including nights?'
            : 'When can you join?',
        a,
      })),
      similar: 6,
    };
    s.applied[app.id] = app;
    if (body.invitationId) s.invites[body.invitationId] = 'accepted';
    save(s);
    return HttpResponse.json(app);
  }),

  http.post('/api/jobs/invitations/:id/:action', async ({ params }) => {
    await delay(400);
    const id = String(params['id']);
    const action = String(params['action']);
    const s = load();
    if (action === 'accept') s.invites[id] = 'accepted';
    if (action === 'decline') s.invites[id] = 'declined';
    save(s);
    const i = invites(s).find((x) => x.id === id);
    return i ? HttpResponse.json(i) : new HttpResponse(null, { status: 404 });
  }),

  http.post('/api/jobs/applications/:id/withdraw', async ({ params }) => {
    await delay(400);
    const id = String(params['id']);
    const s = load();
    if (!s.withdrawn.includes(id)) s.withdrawn.push(id);
    save(s);
    const a = apps(s).find((x) => x.id === id);
    return a ? HttpResponse.json(a) : new HttpResponse(null, { status: 404 });
  }),

  http.get('/api/jobs/alerts', async () => {
    await delay(200);
    return HttpResponse.json(load().alerts);
  }),

  http.put('/api/jobs/alerts', async ({ request }) => {
    await delay(250);
    const s = load();
    s.alerts = (await request.json()) as AlertSettings;
    save(s);
    return HttpResponse.json(s.alerts);
  }),
];

/**
 * Hire Fest H13 "Got an offer?" → My Jobs → Applied (handoff: offers sync). Adds an Offer
 * application for that employer, using a catalogue job when one matches.
 */
export function recordFestOffer(company: string, role: string): void {
  const s = load();
  const j = JOBS.find((x) => x.company === company) ?? JOBS[0];
  if (!j) return;
  const id = `app-fest-${company.toLowerCase().replace(/\W+/g, '-')}`;
  if (s.applied[id]) return;
  s.applied[id] = {
    id,
    job: { ...j, title: role, appliedOn: TODAY },
    appliedOn: '8 Nov',
    updated: 'Offer from Hire Fest',
    cv: 'Yash_Pohare_CV.pdf',
    status: 'offer',
    timeline: timeline('offer', ['8 Nov', '8 Nov', '8 Nov', '8 Nov', TODAY]),
    interview: null,
    answers: [],
    similar: 6,
  };
  save(s);
}
```

## `src/mocks/launchpad.handlers.ts`

```ts
import { assessmentSnapshot } from './assessment.handlers';
import { festActivity } from './fest.handlers';
import { jobsActivity } from './jobs.handlers';
import { HttpResponse, delay, http } from 'msw';
import { isNewLearner, onPersona, sceneOr } from './persona';
import { creditXp } from './xp-ledger';
import { syncLevel } from './quest.handlers';
import { profileProgress, questsWithGaps } from './profile-progress';

/**
 * Career Launchpad v2 (Figma 241:18014). Each card has its own endpoint so one slow or failing
 * service never blocks the page (L6, L7). Page URL scenarios (`?launch=`):
 *   new (L2) · paused (L3) · done (L4) · nofest (L5) · jobs-error (L6) · loading (L7, slow) · error
 * Default is L1 (returning learner).
 */

type Scenario = '' | 'new' | 'paused' | 'done' | 'nofest' | 'jobs-error' | 'loading' | 'error';

function scenario(): Scenario {
  return sceneOr('launch', 'new') as Scenario;
}

let alertsOn = false;

async function wait(base = 350): Promise<void> {
  await delay(scenario() === 'loading' ? 60_000 : base);
}

const MISSIONS = [
  { id: 'assessment', label: 'Take assessment', xp: 100 },
  { id: 'profile', label: 'Finish job profile', xp: 50 },
  { id: 'apply', label: 'Apply to a job', xp: 100 },
  { id: 'fest', label: 'Book a Hire Fest slot', xp: 100 },
];

/** A scenario forced from the page URL (`?launch=`) wins; otherwise the launchpad follows real state. */
const forced = () =>
  typeof location !== 'undefined' && new URLSearchParams(location.search).has('launch');

const LEVEL_NAMES = ['On the launchpad', 'Engines warming up', 'Lift-off', 'Almost in orbit', 'Launched!'];

/**
 * A-306: each mission ticks off from what the learner actually did (assessment played, Job Profile
 * complete, an application sent, a fest slot booked), and the readiness level follows the missions.
 */
function readiness() {
  const s = scenario();
  let done: Record<string, boolean>;
  if (forced() && s === 'done') done = { assessment: true, profile: true, apply: true, fest: true };
  else if (forced() && (s === 'new' || s === 'paused')) done = {};
  else {
    const ca = assessment();
    done = {
      assessment: ca.status === 'done',
      profile: profileProgress().percent === 100,
      apply: jobsActivity().applied > 0,
      fest: festActivity().booked,
    };
  }
  if (!forced()) payMissions(done);
  const missions = MISSIONS.map((m) => ({ ...m, done: !!done[m.id] }));
  const count = missions.filter((m) => m.done).length;
  const total = MISSIONS.reduce((n, m) => n + m.xp, 0);
  const percent = Math.round((missions.reduce((n, m) => n + (m.done ? m.xp : 0), 0) / total) * 100);
  return { level: count, levelName: LEVEL_NAMES[count] ?? LEVEL_NAMES[0], percent, missions };
}

/**
 * A-321: each mission pays its XP once, the first time it is done. The assessment mission is the
 * assessment's own 100 XP (paid per island), so it is never paid twice.
 */
const PAID = 'sm_mock_missions_paid';
function paid(): string[] {
  try {
    return JSON.parse(sessionStorage.getItem(PAID) ?? '[]') as string[];
  } catch {
    return [];
  }
}
function payMissions(done: Record<string, boolean>): void {
  // The sample returning learner's history is already in their 529 XP: start the record from today.
  let stored: string | null = null;
  try {
    stored = sessionStorage.getItem(PAID);
  } catch {
    /* private mode */
  }
  if (stored === null && !isNewLearner()) {
    try {
      sessionStorage.setItem(PAID, JSON.stringify(MISSIONS.filter((m) => done[m.id]).map((m) => m.id)));
    } catch {
      /* private mode */
    }
    return;
  }
  const already = paid();
  const due = MISSIONS.filter((m) => m.id !== 'assessment' && done[m.id] && !already.includes(m.id));
  if (!due.length) return;
  for (const m of due) creditXp(m.xp);
  syncLevel();
  try {
    sessionStorage.setItem(PAID, JSON.stringify([...already, ...due.map((m) => m.id)]));
  } catch {
    /* private mode */
  }
}
onPersona((p) => {
  try {
    if (p === 'new') sessionStorage.setItem(PAID, '[]');
    else sessionStorage.removeItem(PAID);
  } catch {
    /* private mode */
  }
});

/** A-262: the job profile comes from the 8 quests, so its status is read quest by quest. */
function profile() {
  const s = scenario();
  if (s === 'done') return { percent: 100, sectionsLeft: 0, sectionsTotal: 8, stepsLeft: 0, missing: [] };
  const p = profileProgress();
  const missing = questsWithGaps(p);
  return {
    percent: p.percent,
    sectionsLeft: missing.length,
    sectionsTotal: p.quests.length,
    stepsLeft: p.total - p.filled,
    missing,
  };
}

function assessment() {
  const s = scenario();
  // Once the learner has played the real assessment (mock state exists), follow it (A-306).
  const live = forced() ? null : assessmentSnapshot();
  if (live?.done)
    return {
      status: 'done',
      answered: 36,
      total: 36,
      minutes: 0,
      archetype: live.archetype
        ? { name: live.archetype.name, emoji: live.archetype.emoji, traits: live.archetype.traits }
        : null,
      mandatoryCourses: 2,
    };
  if (live && live.answered > 0)
    return {
      status: 'paused',
      answered: live.answered,
      total: 36,
      minutes: Math.max(1, Math.ceil(((36 - live.answered) * 10) / 60)),
      archetype: null,
      mandatoryCourses: 0,
    };
  const archetype = {
    name: 'The Connector',
    emoji: '🤝',
    traits: ['People', 'Helping', 'Structure'],
  };
  if (s === 'new')
    return {
      status: 'not-started',
      answered: 0,
      total: 36,
      minutes: 8,
      archetype: null,
      mandatoryCourses: 0,
    };
  if (s === 'paused')
    return {
      status: 'paused',
      answered: 14,
      total: 36,
      minutes: 5,
      archetype: null,
      mandatoryCourses: 0,
    };
  return { status: 'done', answered: 36, total: 36, minutes: 0, archetype, mandatoryCourses: 2 };
}

function jobs() {
  const s = scenario();
  const act = jobsActivity();
  const top = {
    title: 'Customer Service Agent',
    employer: 'Teleperformance',
    city: 'Pune',
    match: 92,
  };
  // A-261: My Jobs is never locked; a new learner sees their first matches straight away.
  if (s === 'new')
    return {
      locked: false,
      unlockAt: 0,
      matched: 6,
      applied: act.applied,
      saved: act.saved,
      newMatches: act.seen ? 0 : 6,
      interviews: 0,
      top,
    };
  return {
    locked: false,
    unlockAt: 60,
    matched: 12,
    applied: s === 'done' ? 5 : act.applied,
    saved: act.saved || 4,
    newMatches: act.seen ? 0 : 5,
    interviews: s === 'done' ? 2 : 0,
    top,
  };
}

function fest() {
  const s = scenario();
  if (s === 'nofest') return { fest: null, alertsOn };
  return {
    fest: {
      id: 'pune-oct',
      name: 'Pune Hire Fest',
      date: '2026-10-14',
      daysAway: 13,
      recruiters: 40,
      free: true,
      mode: 'Hybrid',
      slotsLeft: 32,
      slotsTotal: 100,
      bookedSlot: s === 'done' || festActivity().booked ? '11:30 AM' : null,
    },
    alertsOn,
  };
}

const S = (
  id: string,
  name: string,
  emoji: string,
  jobs: number,
  newJobs: number,
  fit: number,
) => ({
  id,
  name,
  emoji,
  jobs,
  newJobs,
  fit,
});

const SECTORS = [
  S('bpo', 'BPO', '🎧', 24, 3, 94),
  S('insurance', 'Insurance', '🛡️', 12, 1, 88),
  S('it', 'IT', '💻', 31, 4, 84),
  S('hospitality', 'Hospitality', '🏨', 9, 0, 81),
  S('retail', 'Retail', '🛍️', 18, 2, 79),
  S('banking', 'Banking', '🏦', 15, 1, 76),
  S('healthcare', 'Healthcare', '🏥', 7, 0, 71),
  S('telecom', 'Telecom', '📡', 8, 1, 68),
  S('marketing', 'Marketing', '📣', 10, 0, 64),
  S('manufacturing', 'Manufacturing', '🏭', 13, 2, 58),
  S('engineering', 'Engineering', '⚙️', 11, 0, 52),
  S('pharma', 'Pharmaceutical', '💊', 6, 0, 47),
  S('fashion', 'Fashion Retail', '👗', 5, 1, 44),
  S('ngo', 'NGO', '🌱', 4, 0, 40),
];

function sectors() {
  const noFit = scenario() === 'new' || scenario() === 'paused';
  return SECTORS.map((x) => (noFit ? { ...x, fit: null } : x));
}

const fail = () => scenario() === 'error';

export const launchpadHandlers = [
  http.get('/api/launchpad/readiness', async () => {
    await wait(300);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(readiness());
  }),
  http.get('/api/launchpad/profile', async () => {
    await wait(350);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(profile());
  }),
  http.get('/api/launchpad/assessment', async () => {
    await wait(450);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(assessment());
  }),
  http.get('/api/launchpad/jobs', async () => {
    await wait(600);
    return fail() || scenario() === 'jobs-error'
      ? new HttpResponse(null, { status: 503 })
      : HttpResponse.json(jobs());
  }),
  http.get('/api/launchpad/fest', async () => {
    await wait(500);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(fest());
  }),
  http.get('/api/launchpad/sectors', async () => {
    await wait(400);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(sectors());
  }),
  http.put('/api/launchpad/fest/alerts', async ({ request }) => {
    await delay(300);
    alertsOn = ((await request.json()) as { on: boolean }).on;
    return HttpResponse.json(fest());
  }),
];
```

## `src/mocks/learn.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { creditXp } from './xp-ledger';
import { isNewLearner } from './persona';

/**
 * Learning content mocks: modules (Figma Brain Vita 67:1228), topics with briefs (78:2520) and quizzes
 * (132:2709). Brain Vita and Digital Basics carry the design copy; other modules get generic content.
 * Progress (passed quizzes, bookmarks, feedback, quiz drafts) survives a refresh in the same tab.
 * `?learn=error` in the page URL fails every learn call.
 */

const A = 'assets/learn/';
const ART = ['verbal', 'non-verbal', 'arithmetic', 'data'].map((a) => `${A}topic-${a}.svg`);

interface Q {
  id: string;
  prompt: string;
  options: string[];
  answer: number;
  why: string;
  /** Per wrong option: why that pick is wrong. */
  notes?: Record<number, string>;
  /** Muni's line when this one is missed. */
  muni?: string;
}

interface BriefSeed {
  title: string;
  body: string;
  example?: string;
  keyTerm?: string;
  videoUrl?: string;
  media?: { kind: 'image' | 'video'; src: string; alt: string; duration?: string; poster?: string };
}

interface TopicSeed {
  slug: string;
  title: string;
  art: string;
  briefs: BriefSeed[];
  /** Briefs already passed when the mock starts, with XP each earned. */
  done: number[];
  questions: Q[];
  quizTitle?: string;
  course?: string;
}

interface ModuleSeed {
  slug: string;
  title: string;
  tagline: string;
  knook: { slug: string; title: string };
  topics: TopicSeed[];
}

const generic = (n: number, topic: string): BriefSeed[] =>
  Array.from({ length: n }, (_, i) => ({
    title: `${topic} · Brief ${i + 1}`,
    body: `Short, focused reading for ${topic.toLowerCase()}. Read it once, then take the one-question quiz to lock it in.`,
  }));

const MIRROR: BriefSeed = {
  title: 'Mirror and Water Images',
  body: 'A mirror image is how a figure looks when a mirror is placed beside it: left and right swap, while top and bottom stay the same. A water image is the reflection you see in still water below a figure: top and bottom swap, while left and right stay the same. In these questions you pick the correct reflection from four options. Check one feature at a time, such as a corner, a dot or a letter, and track where it lands after the flip. Rule out any option that breaks even one rule.',
  example:
    'Hold the word “CAB” up to a mirror on its right and you read BAC, with every letter flipped sideways. Reflect the same word in water and the order stays C-A-B, but every letter turns upside down. An option that keeps the right order but does not flip the letters is wrong.',
  keyTerm: 'Lateral inversion',
  videoUrl: 'https://www.youtube.com/results?search_query=mirror+and+water+images+reasoning',
  media: {
    kind: 'image',
    src: `${A}brief-mirror.svg`,
    alt: 'An orange triangle and its mirror image in violet on the other side of a vertical mirror line; the dot moves from the bottom-left corner to the bottom-right.',
  },
};

const EMBEDDED: BriefSeed = {
  title: 'Embedded Figures',
  body: 'An embedded figure is a simple shape hidden inside a more complex one. Look for the shape’s most distinctive edge or angle first, then check that every other line of it is present in the same size and orientation.',
  keyTerm: 'Lateral inversion',
  media: {
    kind: 'image',
    src: `${A}brief-mirror.svg`,
    alt: 'Two triangles either side of a line.',
  },
};

const NON_VERBAL_TITLES = [
  'Series Completion',
  'Analogy',
  'Classification',
  'Odd One Out',
  'Pattern Completion',
  'Figure Matrix',
  'Rule Detection',
  'Dot Situation',
  'Cubes and Dice',
];

const MIRROR_QUIZ: Q[] = [
  {
    id: 'nv-q1',
    prompt: 'A mirror is placed to the right of a figure. What changes in its mirror image?',
    options: [
      'Top and bottom swap',
      'Left and right swap',
      'Nothing changes',
      'The figure turns 90°',
    ],
    answer: 1,
    why: 'A side mirror flips the figure sideways: left and right swap, top and bottom stay where they are.',
    notes: { 0: 'Top and bottom swap in a water image, not a mirror image.' },
    muni: 'So close! A side mirror flips left and right.',
  },
  {
    id: 'nv-q2',
    prompt: 'What does a water image do to a figure?',
    options: [
      'Left and right swap',
      'Top and bottom swap, left and right stay',
      'The figure shrinks',
      'Both directions swap',
    ],
    answer: 1,
    why: 'Still water sits below the figure, so the reflection turns it upside down while keeping left and right.',
    notes: { 0: 'That’s a mirror image. Water reflects from below.' },
    muni: 'Nearly! Water flips it upside down.',
  },
  {
    id: 'nv-q3',
    prompt: 'You hold the word “CAB” up to a mirror on its right. What do you read?',
    options: [
      'CAB, upside down',
      'BAC, with every letter flipped sideways',
      'CAB, unchanged',
      'BAC, upside down',
    ],
    answer: 1,
    why: 'The order reverses and each letter is laterally inverted, which is why it reads BAC with flipped letters.',
    notes: { 0: 'Upside down is the water image of CAB.' },
    muni: 'Almost! Order and letters both flip in a mirror.',
  },
];

const WORKPLACE_QUIZ: Q[] = [
  {
    id: 'dw-q1',
    prompt: 'Which tool is best suited for formal correspondence in a digital workplace?',
    options: ['Social media posts', 'Instant messaging', 'Video conferencing', 'Email'],
    answer: 3,
    why: 'Email is the standard for formal messages: it keeps a written record, can carry attachments and is easy to refer back to later.',
    notes: {
      1: 'Instant messaging is great for quick, casual chats, but it’s informal and messages get buried fast.',
      0: 'Social media is public and informal, so it isn’t the place for formal work messages.',
      2: 'Video calls are for live conversations, not written correspondence.',
    },
    muni: 'So close! Email’s the formal one.',
  },
  {
    id: 'dw-q2',
    prompt: 'What is a key benefit of using instant messaging in a work setting?',
    options: [
      'It allows for quick queries and immediate responses',
      'It requires significant setup time',
      'It is ideal for sending very long documents',
      'It replaces all other communication methods',
    ],
    answer: 0,
    why: 'Instant messaging is built for short questions that need a fast reply, without waiting for an email or booking a meeting.',
    muni: 'Nearly! IM is all about speed.',
  },
  {
    id: 'dw-q3',
    prompt: 'Video conferencing is primarily used for:',
    options: [
      'Sending physical mail',
      'Virtual meetings and face-to-face discussions',
      'Creating spreadsheets',
      'Storing files offline',
    ],
    answer: 1,
    why: 'Video calls let teams in different places meet face to face in real time, share screens and talk things through.',
    muni: 'Close one! Video calls are for meetings.',
  },
];

const genericQuiz = (topic: string): Q[] => [
  {
    id: `${topic}-q1`,
    prompt: 'What’s the best way to lock in what you just read?',
    options: [
      'Skip to the next module',
      'Try the quiz straight away',
      'Read it ten times',
      'Wait a week',
    ],
    answer: 1,
    why: 'Answering right after reading (retrieval practice) is what makes it stick.',
  },
];

const MODULES: ModuleSeed[] = [
  {
    slug: 'brain-vita',
    title: 'Brain Vita',
    tagline: 'Sharpen your reasoning, one quiz at a time.',
    knook: { slug: 'skill', title: 'Skill Knook' },
    topics: [
      {
        slug: 'verbal-reasoning',
        title: 'Verbal Reasoning',
        art: ART[0] ?? '',
        briefs: generic(51, 'Verbal Reasoning'),
        done: Array.from({ length: 51 }, (_, i) => (i % 3 === 2 ? 2 : 1)),
        questions: genericQuiz('verbal'),
      },
      {
        slug: 'non-verbal-reasoning',
        title: 'Non-Verbal Reasoning',
        art: ART[1] ?? '',
        briefs: [
          ...NON_VERBAL_TITLES.map((t) => ({
            title: t,
            body: `${t}: spot the rule that links the figures, then apply it to find the missing one. Work feature by feature and rule options out.`,
          })),
          EMBEDDED,
          MIRROR,
        ],
        done: [1, 1, 0, 1, 1, 1, 1, 1, 1, 1],
        questions: MIRROR_QUIZ,
        course: 'Non-Verbal Reasoning: Full course + certificate',
      },
      {
        slug: 'arithmetical-ability',
        title: 'Arithmetical Ability',
        art: ART[2] ?? '',
        briefs: generic(37, 'Arithmetical Ability'),
        done: Array.from({ length: 37 }, (_, i) => (i < 10 ? 2 : 1)),
        questions: genericQuiz('arith'),
      },
      {
        slug: 'data-interpretation',
        title: 'Data Interpretation',
        art: ART[3] ?? '',
        briefs: generic(6, 'Data Interpretation'),
        done: [1, 1, 0, 1, 0, 1],
        questions: genericQuiz('data'),
      },
    ],
  },
  {
    slug: 'digital-basics',
    title: 'Digital Basics',
    tagline: 'Tools and habits for the modern workplace.',
    knook: { slug: 'course', title: 'Digital Foundations Knook' },
    topics: [
      {
        slug: 'digital-workplace-essentials',
        title: 'Digital Workplace Essentials',
        art: ART[3] ?? '',
        briefs: generic(10, 'Digital Workplace'),
        done: [1, 1, 0, 1],
        questions: WORKPLACE_QUIZ,
        quizTitle: 'Digital Workplace Essentials',
      },
    ],
  },
];

const titleCase = (slug: string) =>
  slug
    .split('-')
    .map((w) => (w[0] ?? '').toUpperCase() + w.slice(1))
    .join(' ');

function moduleSeed(slug: string): ModuleSeed {
  const known = MODULES.find((m) => m.slug === slug);
  if (known) return known;
  const title = titleCase(slug);
  return {
    slug,
    title,
    tagline: `Short briefs and quick quizzes on ${title}.`,
    knook: { slug: 'skill', title: 'Skill Knook' },
    topics: ['Getting Started', 'Going Deeper'].map((t, i) => ({
      slug: t.toLowerCase().replace(/\s+/g, '-'),
      title: t,
      art: ART[i] ?? '',
      briefs: generic(3, t),
      done: i === 0 ? [1] : [],
      questions: genericQuiz(`${slug}-${i}`),
    })),
  };
}

/* ---------- Session state ---------- */

interface LearnState {
  /** `${module}/${topic}` → XP per passed brief (index-aligned). */
  passed: Record<string, number[]>;
  results: Record<string, { correct: number; total: number }>;
  bookmarks: string[];
  helpful: Record<string, boolean | null>;
  drafts: Record<string, Record<string, string>>;
}

const KEY = 'sm_mock_learn';

function load(): LearnState {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (raw) return JSON.parse(raw) as LearnState;
  } catch {
    /* fresh state */
  }
  return { passed: {}, results: {}, bookmarks: [], helpful: {}, drafts: {} };
}

function save(s: LearnState): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    /* private mode — keep in memory only */
  }
}

const passedOf = (s: LearnState, m: string, t: TopicSeed) =>
  s.passed[`${m}/${t.slug}`] ?? (isNewLearner() ? [] : t.done);

function topicSummary(s: LearnState, m: string, t: TopicSeed) {
  const p = passedOf(s, m, t);
  return {
    slug: t.slug,
    title: t.title,
    art: t.art,
    quizzesDone: p.length,
    quizzesTotal: t.briefs.length,
    xp: p.reduce((a, b) => a + b, 0),
  };
}

function scenario(): string {
  return typeof location === 'undefined'
    ? ''
    : (new URLSearchParams(location.search).get('learn') ?? '');
}

const fail = () => new HttpResponse(null, { status: 503 });
const notFound = () => new HttpResponse(null, { status: 404 });

function findTopic(m: string, t: string) {
  const mod = moduleSeed(m);
  const topic = mod.topics.find((x) => x.slug === t);
  return topic ? { mod, topic } : null;
}

const quizId = (m: string, t: string, brief: number) => `${m}~${t}~${brief}`;

function questionsFor(topic: TopicSeed, briefIndex: number): Q[] {
  // The design quiz belongs to the topic's last brief; earlier briefs get a single question.
  return briefIndex === topic.briefs.length - 1 || topic.questions.length > 1
    ? topic.questions
    : topic.questions.slice(0, 1);
}

/** The brief whose quiz is up next (first not passed), or the last one when all are done. */
function quizBrief(passed: number[], topic: TopicSeed): number {
  return Math.min(passed.length, topic.briefs.length - 1);
}

export const learnHandlers = [
  http.get('/api/learn/modules/:module', async ({ params }) => {
    await delay(400);
    if (scenario() === 'error') return fail();
    const s = load();
    const mod = moduleSeed(String(params['module']));
    const topics = mod.topics.map((t) => topicSummary(s, mod.slug, t));
    return HttpResponse.json({
      slug: mod.slug,
      title: mod.title,
      tagline: mod.tagline,
      knook: mod.knook,
      quizzesDone: topics.reduce((a, t) => a + t.quizzesDone, 0),
      quizzesTotal: topics.reduce((a, t) => a + t.quizzesTotal, 0),
      xp: topics.reduce((a, t) => a + t.xp, 0),
      topics,
    });
  }),

  http.get('/api/learn/modules/:module/topics/:topic', async ({ params }) => {
    await delay(400);
    if (scenario() === 'error') return fail();
    const found = findTopic(String(params['module']), String(params['topic']));
    if (!found) return notFound();
    const { mod, topic } = found;
    const s = load();
    const passed = passedOf(s, mod.slug, topic);
    const sum = topicSummary(s, mod.slug, topic);
    const briefs = topic.briefs.map((b, i) => {
      const id = `${mod.slug}~${topic.slug}~${i}`;
      const qid = quizId(mod.slug, topic.slug, i);
      const status = i < passed.length ? 'done' : i === passed.length ? 'current' : 'locked';
      return {
        id,
        index: i + 1,
        title: b.title,
        status,
        body: b.body,
        ...(b.example ? { example: b.example } : {}),
        ...(b.keyTerm ? { keyTerm: b.keyTerm } : {}),
        ...(b.videoUrl ? { videoUrl: b.videoUrl } : {}),
        ...(b.media ? { media: b.media } : {}),
        bookmarked: s.bookmarks.includes(id),
        helpful: s.helpful[id] ?? null,
        xp: passed[i] ?? 0,
        quiz: {
          id: qid,
          questions: questionsFor(topic, i).length,
          result:
            s.results[qid] ?? (i < passed.length ? { correct: passed[i] ?? 0, total: 1 } : null),
        },
      };
    });
    return HttpResponse.json({
      slug: topic.slug,
      title: topic.title,
      module: { slug: mod.slug, title: mod.title },
      knook: { title: mod.knook.title },
      quizzesDone: sum.quizzesDone,
      quizzesTotal: sum.quizzesTotal,
      xp: sum.xp,
      briefs,
      course: topic.course
        ? {
            title: topic.course,
            unlocked: passed.length >= topic.briefs.length,
            link: `/learn/${mod.slug}/${topic.slug}/course`,
          }
        : null,
    });
  }),

  http.put('/api/learn/briefs/:id/bookmark', async ({ params, request }) => {
    await delay(200);
    const id = String(params['id']);
    const { on } = (await request.json()) as { on: boolean };
    const s = load();
    s.bookmarks = on ? [...new Set([...s.bookmarks, id])] : s.bookmarks.filter((b) => b !== id);
    save(s);
    return new HttpResponse(null, { status: 204 });
  }),

  http.put('/api/learn/briefs/:id/feedback', async ({ params, request }) => {
    await delay(200);
    const { helpful } = (await request.json()) as { helpful: boolean | null };
    const s = load();
    s.helpful[String(params['id'])] = helpful;
    save(s);
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/learn/modules/:module/topics/:topic/quiz', async ({ params }) => {
    await delay(400);
    if (scenario() === 'error') return fail();
    const found = findTopic(String(params['module']), String(params['topic']));
    if (!found) return notFound();
    const { mod, topic } = found;
    const s = load();
    const passed = passedOf(s, mod.slug, topic);
    const sum = topicSummary(s, mod.slug, topic);
    const brief = quizBrief(passed, topic);
    const id = quizId(mod.slug, topic.slug, brief);
    return HttpResponse.json({
      id,
      title: topic.quizTitle ?? topic.briefs[brief]?.title ?? topic.title,
      module: { slug: mod.slug, title: mod.title },
      topic: { slug: topic.slug, title: topic.title },
      knook: { title: mod.knook.title },
      quizzesDone: sum.quizzesDone,
      quizzesTotal: sum.quizzesTotal,
      xp: sum.xp,
      questions: questionsFor(topic, brief).map((q) => ({
        id: q.id,
        prompt: q.prompt,
        options: q.options.map((text, i) => ({ id: `${q.id}-o${i}`, text })),
      })),
      draft: s.drafts[id] ?? {},
      alreadyPassed: brief < passed.length,
    });
  }),

  http.put('/api/learn/quizzes/:id/draft', async ({ params, request }) => {
    const { answers } = (await request.json()) as { answers: Record<string, string> };
    const s = load();
    s.drafts[String(params['id'])] = answers;
    save(s);
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/learn/quizzes/:id/submit', async ({ params, request }) => {
    await delay(600);
    if (scenario() === 'error') return fail();
    const id = String(params['id']);
    const [m = '', t = '', b = '0'] = id.split('~');
    const found = findTopic(m, t);
    if (!found) return notFound();
    const { mod, topic } = found;
    const brief = Number(b);
    const { answers } = (await request.json()) as { answers: Record<string, string> };
    const qs = questionsFor(topic, brief);
    const results = qs.map((q) => {
      const correctOptionId = `${q.id}-o${q.answer}`;
      const pickedOptionId = answers[q.id] ?? '';
      const picked = Number(pickedOptionId.split('-o').pop());
      const correct = pickedOptionId === correctOptionId;
      const note = correct ? undefined : q.notes?.[picked];
      return {
        questionId: q.id,
        correctOptionId,
        pickedOptionId,
        correct,
        explanation: q.why,
        ...(note ? { pickNote: note } : {}),
      };
    });
    const correct = results.filter((r) => r.correct).length;
    const s = load();
    const key = `${mod.slug}/${topic.slug}`;
    const passed = [...passedOf(s, mod.slug, topic)];
    // XP only the first time a brief's quiz is checked (+1 per correct, no bonus).
    const first = brief >= passed.length;
    const xp = first ? correct : 0;
    if (first) passed[brief] = xp;
    creditXp(xp);
    s.passed[key] = passed;
    s.results[id] = { correct, total: qs.length };
    s.drafts = Object.fromEntries(Object.entries(s.drafts).filter(([k]) => k !== id));
    save(s);
    const missed = qs.find((q, i) => !results[i]?.correct);
    return HttpResponse.json({
      correct,
      total: qs.length,
      xp,
      results,
      muni:
        correct === qs.length
          ? 'Brain on fire today!'
          : correct === 0
            ? 'Shake it off. Read the why, then go again.'
            : (missed?.muni ?? 'So close! Check the one you missed.'),
    });
  }),
];
```

## `src/mocks/matches.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { environment } from '../environments/environment';
import type { IndustryFit, MatchJob, MatchStatus } from '../app/core/matches/matches.api';

/**
 * My Matches mocks (Figma 200:38000 + MM1–MM6). Jobs appear once the profile is published (Q8 "Send it").
 * Add `?matches=empty` or `?matches=error` to the page URL to see MM2 / MM3.
 */
const KEY = 'sm_mock_published';
let published = (() => {
  try {
    return sessionStorage.getItem(KEY) === '1';
  } catch {
    return false;
  }
})();
const status = new Map<string, MatchStatus>();
/** A confirmed institute claim (V4) shows matches before Vibe Check publishes the profile. */
let claimed = false;
export function markClaimed(): void {
  claimed = true;
}

type Seed = Omit<MatchJob, 'status'> & { readonly start: MatchStatus };

const ZA: readonly Seed[] = [
  {
    id: 'm1',
    title: 'Contact Centre Agent — Learnership',
    employer: 'Ubuntu CX (Pty) Ltd',
    kind: 'learnership',
    location: 'Randburg',
    distanceKm: 4,
    match: 92,
    pay: 'R 6,500 / month stipend · 12 months',
    start: 'invited',
    interviewAt: '2026-10-09T10:00:00',
    industry: 'bpo',
    tipKind: 'boost',
    tip: 'Add Call centre systems proof → +3%',
    skills: [
      { name: 'Customer service', evidence: 'assessed' },
      { name: 'Active listening', evidence: 'declared' },
      { name: 'isiZulu + English', evidence: 'level', note: 'C2 / C1' },
    ],
  },
  {
    id: 'm2',
    title: 'Back Office Assistant',
    employer: 'Sizwe Financial Services',
    kind: 'job',
    location: 'Sandton',
    distanceKm: 11,
    match: 88,
    pay: 'R 8,000 – 9,500 / month',
    start: 'shortlisted',
    interviewAt: null,
    industry: 'finance',
    tipKind: 'boost',
    tip: 'Typing test (5 min) would make Excel Assessed',
    skills: [
      { name: 'Data capturing', evidence: 'verified' },
      { name: 'MS Excel', evidence: 'declared' },
      { name: 'Matric (NSC)', evidence: 'verified' },
    ],
  },
  {
    id: 'm3',
    title: 'Retail Sales Assistant',
    employer: 'Kasi Retail Group',
    kind: 'job',
    location: 'Soweto',
    distanceKm: 18,
    match: 81,
    pay: 'R 5,800 – 6,800 / month',
    start: 'recommended',
    interviewAt: null,
    industry: 'retail',
    tipKind: 'gap',
    tip: 'Weekend shifts required — you said Weekends: no',
    skills: [
      { name: 'Customer service', evidence: 'assessed' },
      { name: 'Conflict resolution', evidence: 'declared' },
    ],
  },
  {
    id: 'm4',
    title: 'Front Desk Trainee (Hospitality)',
    employer: 'Jozi Hotels',
    kind: 'learnership',
    location: 'Rosebank',
    distanceKm: 9,
    match: 76,
    pay: 'R 6,000 / month · learnership',
    start: 'saved',
    interviewAt: null,
    industry: 'hospitality',
    tipKind: 'gap',
    tip: 'Missing: Hospitality NQF 2 (can be done on the job)',
    skills: [
      { name: 'Active listening', evidence: 'declared' },
      { name: 'isiZulu + English', evidence: 'level', note: 'C2 / C1' },
    ],
  },
  {
    id: 'm5',
    title: 'Chat Support Agent',
    employer: 'Ubuntu CX (Pty) Ltd',
    kind: 'job',
    location: 'Remote',
    distanceKm: null,
    match: 74,
    pay: 'R 7,200 / month',
    start: 'recommended',
    interviewAt: null,
    industry: 'bpo',
    tipKind: 'boost',
    tip: 'A typing test would lift this to Strong',
    skills: [{ name: 'Customer service', evidence: 'assessed' }],
  },
  {
    id: 'm6',
    title: 'Stock Controller Assistant',
    employer: 'Mzansi Logistics',
    kind: 'job',
    location: 'Midrand',
    distanceKm: 22,
    match: 64,
    pay: 'R 6,800 / month',
    start: 'recommended',
    interviewAt: null,
    industry: 'logistics',
    tipKind: 'gap',
    tip: 'Missing: Warehousing basics (short course)',
    skills: [{ name: 'Data capturing', evidence: 'verified' }],
  },
];

const IN: readonly Seed[] = [
  {
    id: 'm1',
    title: 'Customer Support Executive — Voice',
    employer: 'Swiggy',
    kind: 'job',
    location: 'Andheri, Mumbai',
    distanceKm: 6,
    match: 92,
    pay: '₹3.2 – 4 LPA',
    start: 'invited',
    interviewAt: '2026-10-09T10:00:00',
    industry: 'bpo',
    tipKind: 'boost',
    tip: 'Add a call-centre certificate → +3%',
    skills: [
      { name: 'Customer service', evidence: 'assessed' },
      { name: 'Communication', evidence: 'declared' },
      { name: 'Hindi + English', evidence: 'level', note: 'C2 / C1' },
    ],
  },
  {
    id: 'm2',
    title: 'Back Office Executive',
    employer: 'HDFC Bank',
    kind: 'job',
    location: 'Powai, Mumbai',
    distanceKm: 11,
    match: 88,
    pay: '₹2.8 – 3.6 LPA',
    start: 'shortlisted',
    interviewAt: null,
    industry: 'finance',
    tipKind: 'boost',
    tip: 'Typing test (5 min) would make Excel Assessed',
    skills: [
      { name: 'Data capturing', evidence: 'verified' },
      { name: 'MS Excel', evidence: 'declared' },
    ],
  },
  {
    id: 'm3',
    title: 'Retail Sales Associate',
    employer: 'Reliance Retail',
    kind: 'job',
    location: 'Malad, Mumbai',
    distanceKm: 14,
    match: 81,
    pay: '₹2.4 – 3 LPA',
    start: 'recommended',
    interviewAt: null,
    industry: 'retail',
    tipKind: 'gap',
    tip: 'Weekend shifts required — you said Weekends: no',
    skills: [{ name: 'Customer service', evidence: 'assessed' }],
  },
  {
    id: 'm4',
    title: 'Hotel Front Office Trainee (NAPS)',
    employer: 'Taj Hotels',
    kind: 'apprenticeship',
    location: 'Colaba, Mumbai',
    distanceKm: 19,
    match: 76,
    pay: '₹15,000 / month stipend',
    start: 'saved',
    interviewAt: null,
    industry: 'hospitality',
    tipKind: 'gap',
    tip: 'Missing: Front office basics (done on the job)',
    skills: [{ name: 'Hindi + English', evidence: 'level', note: 'C2 / C1' }],
  },
  {
    id: 'm5',
    title: 'Chat Support Associate',
    employer: 'Teleperformance',
    kind: 'job',
    location: 'Remote',
    distanceKm: null,
    match: 74,
    pay: '₹2.6 LPA',
    start: 'recommended',
    interviewAt: null,
    industry: 'bpo',
    tipKind: 'boost',
    tip: 'A typing test would lift this to Strong',
    skills: [{ name: 'Customer service', evidence: 'assessed' }],
  },
];

const INDUSTRIES: readonly IndustryFit[] = [
  {
    id: 'bpo',
    name: 'Contact centre & BPO',
    fit: 92,
    why: 'Social + Enterprising interests · strong communication & patience',
    roles: 'Contact centre agent, Chat support',
    path: 'Your contact centre certificate fits',
    pathDone: true,
    jobs: 38,
  },
  {
    id: 'retail',
    name: 'Retail & e-commerce',
    fit: 84,
    why: 'Enjoys helping people, works well under pressure',
    roles: 'Sales assistant, Cashier, Store ops',
    path: 'Short course: Retail basics (8 weeks)',
    pathDone: false,
    jobs: 21,
  },
  {
    id: 'finance',
    name: 'Financial services — operations',
    fit: 78,
    why: 'Detail-focused, good with numbers',
    roles: 'Back office, Data capturer, KYC',
    path: 'Learning path: Banking basics (4 h)',
    pathDone: false,
    jobs: 12,
  },
  {
    id: 'hospitality',
    name: 'Hospitality & tourism',
    fit: 71,
    why: 'Social interests, multilingual',
    roles: 'Front desk, Guest services',
    path: 'Learnership: Hospitality level 2',
    pathDone: false,
    jobs: 9,
  },
  {
    id: 'logistics',
    name: 'Logistics & supply chain admin',
    fit: 64,
    why: 'Organised, reliable',
    roles: 'Stock controller, Dispatch clerk',
    path: 'Short course: Warehousing basics',
    pathDone: false,
    jobs: 6,
  },
];

function force(request: Request): string | null {
  return new URL(request.referrer || 'http://x/').searchParams.get('matches');
}

function jobs(): MatchJob[] {
  const seeds = environment.market === 'ZA' ? ZA : IN;
  return seeds.map(({ start, ...j }) => ({ ...j, status: status.get(j.id) ?? start }));
}

export const matchesHandlers = [
  http.get('/api/me/matches/preview', async () => {
    await delay(500);
    const list = jobs();
    return HttpResponse.json({
      count: 14,
      radiusKm: 25,
      top: list.slice(0, 2).map(({ id, title, kind, match }) => ({ id, title, kind, match })),
    });
  }),

  http.post('/api/me/profile/publish', async () => {
    await delay(900);
    published = true;
    try {
      sessionStorage.setItem(KEY, '1');
    } catch {
      /* private mode: published for this page only */
    }
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/me/matches', async ({ request }) => {
    await delay(700);
    const mode = force(request);
    if (mode === 'error') return new HttpResponse(null, { status: 500 });
    const empty = mode === 'empty' || !(published || claimed);
    const list = empty ? [] : jobs();
    return HttpResponse.json({
      total: empty ? 0 : claimed && !published ? 9 : 14,
      shortlisted: list.filter((j) => j.status === 'shortlisted').length + (empty ? 0 : 1),
      invites: list.filter((j) => j.status === 'invited').length,
      profileComplete: 100,
      visible: published,
      jobs: list,
      industries: INDUSTRIES,
      compass: {
        takenOn: '2026-09-12',
        summary:
          'Top interests: Social, Enterprising · Strengths: communication, patience, reliability. Industry fit = interests + strengths + your skills + local demand.',
      },
    });
  }),

  http.post('/api/me/matches/:id/apply', async ({ params }) => {
    await delay(600);
    status.set(String(params['id']), 'applied');
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/me/matches/:id/save', async ({ params, request }) => {
    await delay(300);
    const { saved } = (await request.json()) as { saved: boolean };
    status.set(String(params['id']), saved ? 'saved' : 'recommended');
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/me/matches/:id/accept', async ({ params }) => {
    await delay(600);
    status.set(String(params['id']), 'accepted');
    return new HttpResponse(null, { status: 204 });
  }),
];
```

## `src/mocks/mock-backend.interceptor.ts`

```ts
import {
  HttpErrorResponse,
  HttpEvent,
  HttpHeaders,
  HttpInterceptorFn,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { Observable, from, switchMap } from 'rxjs';
import { getResponse } from 'msw';
import { handlers } from './handlers';

/**
 * Dev-only mock backend (A-157). Runs the MSW handlers inside the page instead of a service worker, so
 * Chrome stopping an idle worker (or a hard reload) can never make a save fall through to the dev server.
 * Requests no handler claims go on to the network as usual.
 */
export const mockBackendInterceptor: HttpInterceptorFn = (req, next) =>
  from(resolve(req)).pipe(
    switchMap((res): Observable<HttpEvent<unknown>> => (res ? from(toHttp(req, res)) : next(req))),
  );

async function resolve(req: HttpRequest<unknown>): Promise<Response | undefined> {
  const url = new URL(req.urlWithParams, location.href);
  const headers = new Headers();
  for (const key of req.headers.keys()) headers.set(key, req.headers.get(key) ?? '');
  let body: BodyInit | undefined;
  if (req.method !== 'GET' && req.method !== 'HEAD' && req.body !== null) {
    if (req.body instanceof FormData || req.body instanceof Blob || typeof req.body === 'string') {
      body = req.body;
    } else {
      body = JSON.stringify(req.body);
      if (!headers.has('content-type')) headers.set('content-type', 'application/json');
    }
  }
  const request = new Request(url, { method: req.method, headers, body, referrer: location.href });
  return getResponse(handlers, request);
}

async function toHttp(req: HttpRequest<unknown>, res: Response): Promise<HttpEvent<unknown>> {
  const text = await res.text();
  let body: unknown = text || null;
  if (text && (res.headers.get('content-type') ?? '').includes('json')) {
    try {
      body = JSON.parse(text);
    } catch {
      /* leave as text */
    }
  }
  const headers = new HttpHeaders(Object.fromEntries(res.headers.entries()));
  const init = { headers, status: res.status, statusText: res.statusText, url: req.urlWithParams };
  if (res.ok) return new HttpResponse({ ...init, body });
  throw new HttpErrorResponse({ ...init, error: body });
}
```

## `src/mocks/muni-knowledge.ts`

```ts
import { levelFor, LADDER, QUEST_XP, STEP_XP } from '../app/core/quests/levels';
import { totalXp } from './xp-ledger';
import { assessmentSnapshot } from './assessment.handlers';
import { isNewLearner, learnerName } from './persona';
import { profileProgress } from './profile-progress';
import { mockProgress } from './quest.handlers';

/**
 * Muni's platform knowledge (A-282). Stands in for the real assistant: every answer is about the
 * signed-in learner (their name, XP, level, quests, profile gaps) and explains a feature in plain
 * steps with links to the right screen. Keyword-scored, so "how do i get xp" and "earn points"
 * land on the same answer. Swap for the backend when it lands; the reply shape stays the same.
 */

type Block =
  | { kind: 'text'; text: string }
  | { kind: 'list'; items: { title: string; detail: string; tag?: string; url: string }[] }
  | {
      kind: 'stats';
      items: { label: string; value: string; icon: string }[];
      link: { label: string; url: string };
    };

export interface Reply {
  blocks: Block[];
  chips: string[];
}

export interface Me {
  first: string;
  fresh: boolean;
  xp: number;
  level: number;
  rank: string;
  toNext: number;
  questsDone: number;
  profilePct: number;
  gaps: { quest: string; left: string; url: string }[];
  archetype: string | null;
}

export function me(): Me {
  const fresh = isNewLearner();
  const xp = totalXp();
  const l = levelFor(xp);
  const p = profileProgress();
  return {
    first: learnerName().first,
    fresh,
    xp,
    level: l.level,
    rank: l.rank,
    toNext: Math.max(0, l.target - xp),
    questsDone: fresh ? mockProgress.completed : 8,
    profilePct: p.percent,
    gaps: p.quests
      .filter((q) => q.left.length)
      .map((q) => ({
        quest: q.name,
        left: q.left.map((x) => x.label).join(', '),
        url: `/quests/${q.slug}?part=${q.left[0]?.part ?? 1}`,
      })),
    archetype: assessmentSnapshot()?.archetype?.name ?? (fresh ? null : 'The Connector'),
  };
}

const t = (text: string): Block => ({ kind: 'text', text });
const link = (title: string, detail: string, url: string, tag?: string) => ({
  title,
  detail,
  url,
  ...(tag ? { tag } : {}),
});

interface Topic {
  id: string;
  /** Words that point here; longer phrases score higher. */
  keys: string[];
  answer: (m: Me) => Reply;
}

const TOPICS: Topic[] = [
  {
    id: 'xp',
    keys: ['xp', 'points', 'earn', 'experience', 'how do i get', 'level up', 'score'],
    answer: (m) => ({
      blocks: [
        t(
          `XP is how Skillmuni keeps score, ${m.first} ⚡ You’ve got ${m.xp} XP right now, Level ${m.level} · ${m.rank}${m.toNext ? `, ${m.toNext} XP to the next level` : ''}.`,
        ),
        t(
          `Ways to earn it:\n• Quests: +${STEP_XP} XP for every step, +${QUEST_XP} XP when a quest is done (an all-optional step pays once you add something to it)\n• Learning: +1 XP for every right answer in a brief’s quiz\n• Career Drivers assessment: +25 XP per island, 100 XP in all (first time)\n• Your first moves on the Lobby: +50 XP bonus\n• Launchpad missions (once each): finish your job profile +50, first job application +100, first Hire Fest slot +100\n• Today’s quests: the XP shown on each, when you clear it\n• Profile: +5 XP for your first photo, +10 XP for your top skills\n• Programme bonus quest: +100 XP\n• Monthly feedback: +5 XP\nXP never expires, and it’s what ranks you in the Arena.`,
        ),
        {
          kind: 'list',
          items: [
            link('Keep learning', 'Short briefs + quick quizzes', '/home', '+1 XP each'),
            link('See your XP and badges', 'My Stats · Rewards dashboard', '/stats'),
          ],
        },
      ],
      chips: ['How do levels work?', 'XP vs credits', 'How does the Arena work?'],
    }),
  },
  {
    id: 'levels',
    keys: ['level', 'levels', 'rank', 'rookie', 'pathfinder', 'trailblazer', 'legend', 'icon'],
    answer: (m) => ({
      blocks: [
        t(
          `Levels go up as your XP grows. You’re Level ${m.level} · ${m.rank}.\n${LADDER.map((l) => `• Level ${l.level} ${l.rank}: from ${l.min} XP`).join('\n')}`,
        ),
        t(
          m.toNext
            ? `${m.toNext} XP to go. A couple of quizzes and a quest step get you most of the way 💪`
            : 'You’re at the top of the ladder. Legend behaviour 👑',
        ),
      ],
      chips: ['How do I earn XP?', 'How does the Arena work?'],
    }),
  },
  {
    id: 'currency',
    keys: ['coin', 'coins', 'credit', 'credits', 'wallet', 'currency', 'money', 'spend'],
    answer: () => ({
      blocks: [
        t(
          'Three things to collect, each with its own job:\n• ⚡ XP: your score. Levels you up and ranks you in the Arena. Never spent.\n• Coins and the rewards store are coming later. For now, everything you do pays XP.\n• 🎓 Credits: earned by finishing courses and certificates. They show your learning on your CV.',
        ),
        {
          kind: 'list',
          items: [link('Your wallet', 'XP and credits in one place', '/stats')],
        },
      ],
      chips: ['How do I earn XP?', 'What are daily quests?'],
    }),
  },
  {
    id: 'quests',
    keys: ['quest', 'quests', 'registration', 'register', 'onboarding', 'who dis', 'steps', 'skip'],
    answer: (m) => ({
      blocks: [
        t(
          `The 8 quests are how you register and build your job profile 🗺️ You’ve finished ${m.questsDone} of 8.\n• Each quest has 1–3 short steps (+${STEP_XP} XP each) and a badge at the end (+${QUEST_XP} XP)\n• Everything autosaves; “Save & exit” picks up exactly where you left\n• Optional steps can be skipped and filled later from My Job Profile`,
        ),
        {
          kind: 'list',
          items: [link('Open your quests', 'See which ones are done', '/welcome-back')],
        },
      ],
      chips: ['What’s left in my profile?', 'How do I earn XP?'],
    }),
  },
  {
    id: 'profile',
    keys: ['job profile', 'profile', 'cv', 'resume', 'left', 'missing', 'complete', 'fill', 'gaps'],
    answer: (m) => ({
      blocks: [
        t(
          m.gaps.length
            ? `Your job profile is ${m.profilePct}% done, ${m.first}. It’s built from your quests, so filling a gap is one tap away:`
            : `Your job profile is 100% done, ${m.first} 🎉 Recruiters see everything you shared, and your CV updates itself.`,
        ),
        ...(m.gaps.length
          ? [
              {
                kind: 'list' as const,
                items: m.gaps.map((g) => link(g.quest, `Left: ${g.left}`, g.url, 'Fill')),
              },
            ]
          : []),
        t(
          'Tip: on My Job Profile, “Edit” on any section opens just that section, already filled in, with Save to bring you straight back.',
        ),
      ],
      chips: ['How does job matching work?', 'Is my ID safe?'],
    }),
  },
  {
    id: 'jobs',
    keys: ['job', 'jobs', 'match', 'matching', 'apply', 'application', 'hire', 'hiring', 'work'],
    answer: (m) => ({
      blocks: [
        t(
          `My Jobs has four tabs: Available, Matching, Invitations and Applied.\n• Matching ranks jobs by your skills, education, languages, where you want to work and your Career Drivers\n• It’s never locked; the fuller your profile (yours is ${m.profilePct}%), the sharper the matches\n• Apply in two taps with your Skillmuni CV, then track it in Applied`,
        ),
        {
          kind: 'list',
          items: [
            link(
              'Customer Support Executive',
              'Teleperformance · Mumbai',
              '/jobs/matching',
              '92% match',
            ),
            link('Retail Associate', 'Reliance Trends · Pune', '/jobs/matching', '86% match'),
          ],
        },
      ],
      chips: ['What’s Hire Fest?', 'What’s left in my profile?'],
    }),
  },
  {
    id: 'fest',
    keys: ['hire fest', 'fest', 'career fest', 'recruiter', 'interview', 'slot', 'book', 'ticket'],
    answer: () => ({
      blocks: [
        t(
          'Hire Fest is a recruitment day: meet employers, interview on the spot 🎪\n• Pick a fest, choose employers and time slots, and get one ticket for the day\n• You need a job profile at least 60% done to book\n• Reschedule or cancel anytime from My bookings',
        ),
        { kind: 'list', items: [link('Upcoming fests', 'Book a slot', '/hire-fest')] },
      ],
      chips: ['How does job matching work?', 'What’s left in my profile?'],
    }),
  },
  {
    id: 'assessment',
    keys: ['assessment', 'career drivers', 'archetype', 'personality', 'test', 'compass'],
    answer: (m) => ({
      blocks: [
        t(
          m.archetype
            ? `Your archetype is ${m.archetype}. It’s the mix of what drives you at work, and it powers your job matches.`
            : 'Career Drivers is 36 quick this-or-that picks (about 12 minutes). No right answers, just you.',
        ),
        t(
          '• Lean left or right on each pair; you can undo\n• At the end you get your archetype, your top drivers and where you’ll thrive\n• It’s saved as you go, so you can pause anytime',
        ),
        {
          kind: 'list',
          items: [
            link(
              m.archetype ? 'See your full results' : 'Start Career Drivers',
              m.archetype ? 'Drivers, thrive zones, courses' : '36 picks · 6 min',
              m.archetype ? '/assessments/career/report' : '/assessments/career',
            ),
          ],
        },
      ],
      chips: ['How does job matching work?', 'How do I earn XP?'],
    }),
  },
  {
    id: 'arena',
    keys: ['arena', 'leaderboard', 'top', 'crown', 'batch', 'league', 'compete', 'friends'],
    answer: (m) => ({
      blocks: [
        t(
          `The Arena ranks learners by XP 🏆\n• Switch between Everyone, My batch and Friends, and This week, This month or All time\n• The weekly board resets every Monday at midnight\n• Top 3 each week move up a league\nYou have ${m.xp} XP in total.`,
        ),
        {
          kind: 'stats',
          items: [
            { label: 'Total XP', value: String(m.xp), icon: 'zap' },
            { label: 'Level', value: String(m.level), icon: 'trophy' },
          ],
          link: { label: 'Open the Arena', url: '/arena' },
        },
      ],
      chips: ['How do I earn XP?', 'How do streaks work?'],
    }),
  },
  {
    id: 'streak',
    keys: ['streak', 'daily', 'every day', 'fire', 'flame'],
    answer: () => ({
      blocks: [
        t(
          'A streak counts the days in a row you learn 🔥\n• Finish at least one brief’s quiz a day to keep it alive\n• Streaks unlock streak badges\n• Miss a day and it goes back to 0, so pick a time that works for you',
        ),
      ],
      chips: ['What are daily quests?', 'How do badges work?'],
    }),
  },
  {
    id: 'daily',
    keys: ['daily quest', 'today', 'mystery chest', 'challenge'],
    answer: () => ({
      blocks: [
        t(
          'Today’s quests are three small goals that reset every night: finish a module, learn for 15 minutes, score 80%+ on a quiz. Each pays the XP shown when you clear it that day. Chest rewards are coming soon 🎁',
        ),
        { kind: 'list', items: [link('Today’s quests', 'My Stats · Rewards', '/stats')] },
      ],
      chips: ['XP vs credits', 'How do streaks work?'],
    }),
  },
  {
    id: 'badges',
    keys: ['badge', 'badges', 'showcase', 'pin', 'achievement', 'unlock'],
    answer: () => ({
      blocks: [
        t(
          'Badges mark milestones: quests, learning, quizzes, streaks and more 🏅\n• Each quest gives one when you finish it\n• “Closest to unlock” on My Stats shows what you’re near\n• Pin up to 3 to your showcase so they show on your Player ID',
        ),
        { kind: 'list', items: [link('My badges', 'All 48, with progress', '/stats/badges')] },
      ],
      chips: ['How do I earn XP?', 'How do streaks work?'],
    }),
  },
  {
    id: 'learn',
    keys: [
      'learn',
      'learning',
      'module',
      'modules',
      'knook',
      'brief',
      'briefs',
      'quiz',
      'course',
      'brain vita',
    ],
    answer: () => ({
      blocks: [
        t(
          'Learning lives in Knooks (shelves of modules) on the Lobby 📚\n• Each module has short briefs (about 2 minutes each)\n• Each brief ends with a quick quiz: +1 XP per right answer\n• Retrying a passed quiz is practice: no XP, but it locks the learning in',
        ),
        {
          kind: 'list',
          items: [
            link('Brain Vita', 'Pick up where you left off', '/learn/brain-vita'),
            link('All Skill Knook modules', 'Browse everything', '/knooks/skill-knook'),
          ],
        },
      ],
      chips: ['How do I earn XP?', 'How do streaks work?'],
    }),
  },
  {
    id: 'id',
    keys: ['aadhaar', 'digilocker', 'id', 'pan', 'passport', 'privacy', 'safe', 'data', 'secure'],
    answer: () => ({
      blocks: [
        t(
          'Your ID is safe 🔒\n• We never store your Aadhaar, PAN or licence number, or a copy of the document\n• You link it through DigiLocker (Government of India), and only the last 4 characters stay with us\n• Mobile and email stay masked until you tap the eye, and employers never see your ID',
        ),
        { kind: 'list', items: [link('Private details', 'Profile · Edit profile', '/profile')] },
      ],
      chips: ['How do I change my mobile?', 'What’s left in my profile?'],
    }),
  },
  {
    id: 'account',
    keys: [
      'change',
      'mobile',
      'email',
      'phone',
      'number',
      'photo',
      'picture',
      'edit',
      'gender',
      'birthday',
      'dob',
    ],
    answer: () => ({
      blocks: [
        t(
          'All in Profile → Edit profile ✏️\n• Mobile or email: type the new one, we send a 6-digit code to it, and it saves once the code matches\n• Gender and date of birth: pick and save\n• Photo: tap the camera on your Player ID card; it shows in the top bar right away',
        ),
        { kind: 'list', items: [link('Edit profile', 'Private details', '/profile')] },
      ],
      chips: ['Is my ID safe?', 'How do I earn XP?'],
    }),
  },
  {
    id: 'sound',
    keys: ['sound', 'voice', 'mute', 'audio', 'speak', 'talk', 'quiet'],
    answer: () => ({
      blocks: [
        t(
          'Sound effects can be turned on or off in Profile → Settings 🔊 It remembers your choice on this device.',
        ),
      ],
      chips: ['What can you do?'],
    }),
  },
  {
    id: 'muni',
    keys: [
      'what can you do',
      'help',
      'who are you',
      'muni',
      'features',
      'how does this work',
      'guide',
    ],
    answer: (m) => ({
      blocks: [
        t(
          `I’m Muni, your Skillmuni buddy 🤙 Ask me anything about the platform, ${m.first}. I’m best at:\n• Your progress: XP, level, rank, badges\n• Your job profile and what’s left to fill\n• Jobs, matching and Hire Fest\n• Learning, quizzes and streaks\n• Privacy, your ID and account settings`,
        ),
      ],
      chips: ['How do I earn XP?', 'What’s left in my profile?', 'How does job matching work?'],
    }),
  },
];

const words = (s: string) => s.toLowerCase().replace(/[^\p{L}\p{N}\s]/gu, ' ');

/** Best topic for a question, or null when nothing fits well enough. */
export function answer(question: string): Reply | null {
  const q = ` ${words(question).replace(/\s+/g, ' ').trim()} `;
  let best: { topic: Topic; score: number } | null = null;
  for (const topic of TOPICS) {
    let score = 0;
    for (const k of topic.keys)
      if (q.includes(` ${k} `) || (k.includes(' ') && q.includes(k)))
        score += k.split(' ').length * 2;
    // Plurals and stems: "badges" vs "badge", "matching" vs "match".
    for (const k of topic.keys) if (!k.includes(' ') && k.length > 3 && q.includes(k)) score += 1;
    if (score && (!best || score > best.score)) best = { topic, score };
  }
  return best ? best.topic.answer(me()) : null;
}

export function greeting(): Reply {
  const m = me();
  return {
    blocks: [
      t(
        `Heyyy ${m.first} 👋 You’re Level ${m.level} · ${m.rank} with ${m.xp} XP. Ask me anything about Skillmuni: XP, jobs, your profile, Hire Fest, the Arena…`,
      ),
    ],
    chips: ['What can you do?', 'How do I earn XP?', 'What’s left in my profile?'],
  };
}

export function fallback(): Reply {
  const m = me();
  return {
    blocks: [
      t(
        `Hmm, I don’t have that one yet, ${m.first} 🤔 Try asking it another way, or pick one of these. I know the platform inside out.`,
      ),
    ],
    chips: ['What can you do?', 'How do I earn XP?', 'How does job matching work?'],
  };
}
```

## `src/mocks/muni.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { answer, fallback, greeting, me } from './muni-knowledge';
import { learnerName } from './persona';

/**
 * Muni chat mocks (Figma Muni Chat v2 179:5915). Keyword intents stand in for the real backend
 * (handoff "to confirm": which intents exist). Replies are rich cards + short bubbles + ≤ 3 chips.
 * `?muni=error` in the page URL fails replies.
 */

const PROFILE = {
  email: 'yash.pohare@thegamification.com',
  mobile: '+91 98765 40697',
};

const mask = {
  email: (e: string) => `${e[0] ?? ''}••••@${e.split('@')[1] ?? ''}`,
  mobile: (m: string) => `••••••${m.replace(/\D/g, '').slice(-4)}`,
};

const text = (t: string) => ({ kind: 'text', text: t });

const TOPIC_CHIPS: Record<string, { intro: string; chips: string[] }> = {
  jobs: {
    intro: 'Jobs it is 💼 What do you want to do?',
    chips: ['Jobs for me', 'Applications', 'Career Compass'],
  },
  learn: {
    intro: 'Let’s learn something 📚 Pick one:',
    chips: ['Continue my quest', 'Quick quiz', 'Browse modules'],
  },
  assess: {
    intro: 'Time to flex 🎯 Which one?',
    chips: ['Career assessment', 'Skill check'],
  },
  me: {
    intro: 'All about you ✨',
    chips: ['Show my profile', 'My XP & rank', 'My career archetype'],
  },
};

function reply(input: { text: string; intent?: string }) {
  const q = (input.intent ?? input.text).toLowerCase().trim();
  const topic = TOPIC_CHIPS[q];
  if (topic) return { blocks: [text(topic.intro)], chips: topic.chips };
  // A-282: greetings and platform questions are answered for this learner.
  if (/^(hi|hey|hello|yo|hii+|namaste)\b/.test(q) && q.split(' ').length <= 3) return greeting();
  const isChip =
    /^(show my profile|my xp & rank|jobs for me|continue my quest|my career archetype|quick quiz|browse modules|applications|career compass|take the career assessment|why do i match\?)$/.test(
      q,
    );
  if (!isChip) return answer(q) ?? fallback();

  if (/profile|about me|my details/.test(q)) {
    return {
      blocks: [
        text('Here’s you on Skillmuni 👇 Private bits stay masked. Tap the eye to peek.'),
        {
          kind: 'profile',
          initials: learnerName().initials,
          name: learnerName().full,
          subtitle: `${me().rank} · Lvl ${me().level}`,
          fields: [
            { key: 'email', label: 'Email', value: mask.email(PROFILE.email), masked: true },
            { key: 'mobile', label: 'Mobile', value: mask.mobile(PROFILE.mobile), masked: true },
            { key: 'since', label: 'Member since', value: 'Apr 2025', masked: false },
          ],
          completeness: me().profilePct,
          nextStep: me().gaps[0]
            ? `Next: ${me().gaps[0]?.quest} · ${me().gaps[0]?.left}`
            : 'All done. Nice!',
        },
        text('Want the tea on your XP, rank or career archetype? 👀'),
      ],
      chips: ['My XP & rank', 'My career archetype', 'Jobs for me'],
    };
  }
  if (/xp|rank|leaderboard|arena/.test(q)) {
    return {
      blocks: [
        text(`You’re cooking, ${me().first} 🔥 Here’s where you stand:`),
        {
          kind: 'stats',
          items: [
            { label: 'Total XP', value: String(me().xp), icon: 'zap' },
            { label: 'Level', value: `${me().level} · ${me().rank}`, icon: 'trophy' },
            { label: 'To next level', value: String(me().toNext), icon: 'trend-up' },
          ],
          link: { label: 'Open the Arena', url: '/arena' },
        },
        text('One module and a quiz gets you a good chunk of the way. Want me to pick one?'),
      ],
      chips: ['Continue my quest', 'Quick quiz', 'Jobs for me'],
    };
  }
  if (/job|match|work|hire|application/.test(q)) {
    return {
      blocks: [
        text('2 jobs match your profile right now 👀'),
        {
          kind: 'list',
          items: [
            {
              title: 'Customer Support Executive',
              detail: 'Teleperformance · Mumbai · ₹22k/mo',
              tag: '92% match',
              url: '/matches',
            },
            {
              title: 'Retail Associate',
              detail: 'Reliance Trends · Pune · ₹18k/mo',
              tag: '86% match',
              url: '/matches',
            },
          ],
        },
      ],
      chips: ['Why do I match?', 'Applications', 'Career Compass'],
    };
  }
  if (/archetype|compass|career/.test(q)) {
    return {
      blocks: [
        text(
          me().archetype
            ? `Your archetype is ${me().archetype} 🤝 It’s what drives you at work, and it shapes your job matches.`
            : 'You haven’t taken Career Drivers yet. 36 quick picks (6 min) and I’ll tell you your archetype 🎯',
        ),
      ],
      chips: ['Jobs for me', 'Take the Career assessment', 'My XP & rank'],
    };
  }
  if (/quest|continue|module|learn|brain|quiz|brief/.test(q)) {
    return {
      blocks: [
        text('Brain Vita is 98.86% done. One quiz gets you the badge 🧠'),
        {
          kind: 'list',
          items: [
            {
              title: 'Non-Verbal Reasoning',
              detail: 'Brief 11 of 11 · 3 questions',
              tag: '+1 XP each',
              url: '/learn/brain-vita/non-verbal-reasoning/quiz',
            },
          ],
        },
      ],
      chips: ['Browse modules', 'My XP & rank', 'Jobs for me'],
    };
  }
  if (/streak/.test(q)) {
    return {
      blocks: [text('2-day streak and counting 🔥 One brief’s quiz today keeps it alive.')],
      chips: ['Quick quiz', 'Continue my quest'],
    };
  }
  if (/cv|resume|attach/.test(q)) {
    return {
      blocks: [
        text(
          'Got your CV 📄 I’ll compare it with your profile and show you what’s new before anything changes.',
        ),
      ],
      chips: ['Show my profile', 'Jobs for me'],
    };
  }
  if (/hi|hey|hello|yo\b/.test(q)) {
    return {
      blocks: [text('Heyyy 👋 Ask me about jobs, learning, your rank or your profile.')],
      chips: ['Jobs for me', 'Continue my quest', 'My XP & rank'],
    };
  }
  return {
    blocks: [
      text(
        'Hmm, I’m not sure about that one yet 🤔 I’m best with jobs, learning, assessments and your profile.',
      ),
    ],
    chips: ['Jobs for me', 'Continue my quest', 'Show my profile'],
  };
}

function scenario(): string {
  return typeof location === 'undefined'
    ? ''
    : (new URLSearchParams(location.search).get('muni') ?? '');
}

export const muniHandlers = [
  http.get('/api/muni/welcome', async () => {
    await delay(300);
    const m = me();
    return HttpResponse.json({
      firstName: m.first,
      intro: `Level ${m.level} · ${m.rank}, ${m.xp} XP. Ask me anything about Skillmuni, or start with what I’d do right now:`,
      suggestions: m.fresh
        ? [
            {
              id: 'drivers',
              icon: 'compass',
              tone: 'accent',
              title: 'Discover your Career Drivers',
              detail: '36 quick picks · 6 min',
              cta: 'Start',
              link: '/assessments/career',
            },
            {
              id: 'learn',
              icon: 'play',
              tone: 'success',
              title: 'Start your first learning module',
              detail: '2-minute brief, then a quick quiz',
              cta: 'Let’s go',
              link: '/learn/brain-vita',
            },
            {
              id: 'jobs',
              icon: 'briefcase',
              tone: 'brand',
              title: 'See jobs that fit you',
              detail: 'Matched to your quests',
              cta: 'See jobs',
              intent: 'Jobs for me',
            },
          ]
        : [
            {
              id: 'finish',
              icon: 'play',
              tone: 'success',
              title: 'Finish Brain-Vita',
              detail: '98.86% done · 1 module to the badge',
              cta: 'Let’s go',
              link: '/learn/brain-vita/non-verbal-reasoning/quiz',
            },
            {
              id: 'jobs',
              icon: 'briefcase',
              tone: 'accent',
              title: '2 jobs match your profile',
              detail: 'Customer Support Exec · Retail Associate',
              cta: 'See jobs',
              intent: 'Jobs for me',
            },
            {
              id: 'streak',
              icon: 'flame',
              tone: 'brand',
              title: 'Keep your 2-day streak',
              detail: '5 min today keeps it alive',
              cta: 'Quick quiz',
              link: '/learn/brain-vita/non-verbal-reasoning/quiz',
            },
          ],
      topics: [
        { id: 'jobs', icon: 'briefcase', label: 'Jobs', options: 5 },
        { id: 'learn', icon: 'play', label: 'Learn', options: 4 },
        { id: 'assess', icon: 'target', label: 'Assess', options: 2 },
        { id: 'me', icon: 'user', label: 'Me', options: 4 },
      ],
      unread: 2,
      nudge: { title: '2 new jobs match you 👀', body: 'Tap Muni to see them' },
    });
  }),

  http.post('/api/muni/messages', async ({ request }) => {
    await delay(900);
    if (scenario() === 'error') return new HttpResponse(null, { status: 503 });
    const body = (await request.json()) as { text: string; intent?: string };
    return HttpResponse.json(reply(body));
  }),

  http.post('/api/muni/reveal', async ({ request }) => {
    await delay(300);
    const { field } = (await request.json()) as { field: 'email' | 'mobile' };
    return HttpResponse.json({ value: PROFILE[field] });
  }),

  http.put('/api/muni/messages/:id/rating', () => new HttpResponse(null, { status: 204 })),

  http.get('/api/muni/history', async () => {
    await delay(300);
    return HttpResponse.json([
      {
        id: 'h1',
        title: 'Jobs for me',
        when: 'Yesterday',
        messages: [
          { from: 'me', text: 'Jobs for me' },
          { from: 'muni', text: '2 jobs match your profile right now 👀' },
        ],
      },
      {
        id: 'h2',
        title: 'My XP & rank',
        when: 'Mon',
        messages: [
          { from: 'me', text: 'My XP & rank' },
          { from: 'muni', text: 'You’re #3 this week with 41 XP. Keep going!' },
        ],
      },
    ]);
  }),
];
```

## `src/mocks/persona.ts`

```ts
/**
 * Who the mock backend is serving (A-232). A real backend knows this from the account; the mock
 * decides at sign-in:
 *   - "Join the squad" (register → verify email) → a brand-new learner. Every feature starts
 *     empty: no assessment, no applications, no bookings, no badges, no streak.
 *   - Logging in with mobile or email → a returning learner with history (the Figma sample data).
 * Deep links without signing in (dev, e2e, Storybook) keep the returning learner.
 * A `?<feature>=` scenario param in the page URL still wins over the persona.
 */
export type Persona = 'new' | 'existing';

const KEY = 'sm_mock_persona';
const PREFIX = 'sm_mock_';
const NAME = 'sm_persona_name';
const listeners: ((p: Persona) => void)[] = [];

export function persona(): Persona {
  try {
    return sessionStorage.getItem(KEY) === 'new' ? 'new' : 'existing';
  } catch {
    return 'existing';
  }
}

export const isNewLearner = (): boolean => persona() === 'new';

/** Switch persona. A new learner wipes every mock store so nothing from a previous account leaks in. */
export function setPersona(p: Persona): void {
  try {
    if (p === 'new') {
      for (const k of Object.keys(sessionStorage))
        if (k.startsWith(PREFIX)) sessionStorage.removeItem(k);
    }
    sessionStorage.setItem(KEY, p);
  } catch {
    /* private mode */
  }
  for (const fn of listeners) fn(p);
}

/** The registered learner's name (new persona) or the Figma sample learner. */
export function learnerName(): {
  first: string;
  last: string;
  full: string;
  initials: string;
  email: string | null;
} {
  let first = 'Yash';
  let last = 'Pohare';
  let email: string | null = null;
  try {
    const raw = isNewLearner() ? sessionStorage.getItem(NAME) : null;
    if (raw)
      ({
        first,
        last,
        email = null,
      } = JSON.parse(raw) as { first: string; last: string; email?: string });
  } catch {
    /* private mode */
  }
  const full = `${first} ${last}`.trim();
  const initials = full
    .split(/\s+/)
    .map((w) => w[0] ?? '')
    .join('')
    .slice(0, 2)
    .toUpperCase();
  return { first, last, full, initials, email };
}

export function rememberName(first: string, last: string, email: string): void {
  try {
    sessionStorage.setItem(NAME, JSON.stringify({ first, last, email }));
  } catch {
    /* private mode */
  }
}

/** Mocks that keep in-memory state reset themselves here. */
export function onPersona(fn: (p: Persona) => void): void {
  listeners.push(fn);
}

/** `?param=` from the page URL, else the persona's default scenario. */
export function sceneOr(param: string, forNew: string): string {
  if (typeof location === 'undefined') return '';
  const v = new URLSearchParams(location.search).get(param);
  if (v !== null) return v;
  return isNewLearner() ? forNew : '';
}

/**
 * A-308: logins that exist beyond the sample learner — emails that signed up or used Google, and
 * mobiles verified in quest 1 or the profile. Kept outside the `sm_mock_` prefix so a new persona's
 * wipe doesn't forget them. Phones are stored as their last 10 digits.
 */
const ACCOUNTS = 'sm_accounts';

export function knownLogins(): string[] {
  try {
    return JSON.parse(sessionStorage.getItem(ACCOUNTS) ?? '[]') as string[];
  } catch {
    return [];
  }
}

export function rememberLogin(value: string): void {
  const v = value.includes('@') ? value.trim().toLowerCase() : value.replace(/\D/g, '').slice(-10);
  try {
    sessionStorage.setItem(ACCOUNTS, JSON.stringify([...new Set([...knownLogins(), v])]));
  } catch {
    /* private mode */
  }
}
```

## `src/mocks/profile-progress.ts`

```ts
import { QUESTS } from '../app/core/quests/quest-catalog';
import { QUEST_SCREENS } from '../app/features/quests/quest-defs';
import { isNewLearner } from './persona';
import { mockProgress, questAnswers } from './quest.handlers';

/**
 * A-262: the job profile is built from the 8 registration quests, so its completeness is read quest
 * by quest: every step counts, and a step the learner skipped (optional ones) is what's "left".
 */
export interface QuestStep {
  readonly key: string;
  readonly label: string;
  /** 1-based step, for `?part=` deep links into the quest. */
  readonly part: number;
}

export interface QuestStatus {
  readonly id: number;
  readonly slug: string;
  readonly name: string;
  readonly filled: number;
  readonly total: number;
  readonly left: readonly QuestStep[];
}

export interface ProfileProgress {
  readonly percent: number;
  readonly filled: number;
  readonly total: number;
  readonly quests: readonly QuestStatus[];
}

/** The sample returning learner finished every quest but skipped three optional steps. */
const SAMPLE_SKIPPED: Readonly<Record<string, readonly string[]>> = {
  'hit-me-up': ['backup'],
  'big-brain-stuff': ['timeline'],
  'your-flex-era': ['trophies'],
};

const clean = (label: string) => label.replace(/\s*(·\s*optional|\(optional\))\s*$/i, '').trim();

export function profileProgress(): ProfileProgress {
  const fresh = isNewLearner();
  const quests = QUESTS.map((q, i): QuestStatus => {
    const parts = QUEST_SCREENS[q.slug]?.parts ?? [];
    const answers = questAnswers(q.slug);
    const left = parts
      .map((p, j) => ({ key: p.key, label: clean(p.label), part: j + 1 }))
      .filter((p) => {
        if (!fresh) return SAMPLE_SKIPPED[q.slug]?.includes(p.key) ?? false;
        if (i + 1 > mockProgress.completed) {
          // Quest in progress: steps past the last one saved; later quests: everything.
          return i === mockProgress.completed ? p.part > mockProgress.partsDone : true;
        }
        return answers[`${p.key}Skipped`] === true;
      });
    return {
      id: q.id,
      slug: q.slug,
      name: q.name,
      filled: parts.length - left.length,
      total: parts.length,
      left,
    };
  });
  const total = quests.reduce((n, q) => n + q.total, 0);
  const filled = quests.reduce((n, q) => n + q.filled, 0);
  return { percent: total ? Math.round((filled / total) * 100) : 0, filled, total, quests };
}

/** Quest names that still have gaps, for chips ("Hit Me Up", "Your Flex Era"…). */
export function questsWithGaps(p = profileProgress()): string[] {
  return p.quests.filter((q) => q.left.length).map((q) => q.name);
}
```

## `src/mocks/profile.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { creditXp, totalXp } from './xp-ledger';
import { levelFor } from '../app/core/quests/levels';
import { isNewLearner, learnerName, rememberLogin, sceneOr } from './persona';
import { assessmentSnapshot } from './assessment.handlers';
import { badgeList, earnedBadges, showcase, upNext } from './badges.data';

/**
 * Profile v2 · Player ID (Figma 186:6669, note 187:8642). Sample data from the design.
 * Strength weights: contact 30 · name 30 · photo 10 · skills 15 · Career Drivers 15 (60% in the design).
 * `?profile=error` in the page URL fails the load; `?profile=new` starts from a fresh learner.
 * Edits live in sessionStorage; an uploaded photo lives in memory only (gone on reload).
 */

type Gender = 'female' | 'male' | 'non-binary' | 'rather-not';

interface State {
  skills: string[];
  gender: Gender;
  dob: string;
  /** A-273: set once a new mobile / email is verified with a code. */
  mobile?: string;
  email?: string;
}

function currentMobile(s: State): string {
  return s.mobile ?? (isNewLearner() ? '' : MOBILE);
}
function currentEmail(s: State): string {
  return s.email ?? learnerName().email ?? EMAIL;
}
function maskMobile(m: string): string {
  return m ? `•••••• ${m.replace(/\D/g, '').slice(-4)}` : 'Not added';
}
/** A-272: the DigiLocker link holds only the masked number. */
function linkedId(): { doc: string; masked: string } | null {
  try {
    return JSON.parse(sessionStorage.getItem('sm_mock_identity') ?? 'null') as {
      doc: string;
      masked: string;
    } | null;
  } catch {
    return null;
  }
}
const DOC_LABEL: Record<string, string> = {
  aadhaar: 'Aadhaar',
  pan: 'PAN',
  'driving-licence': 'Driving licence',
};
const pending: Record<string, string> = {};

const KEY = 'sm_mock_profile';
const WEIGHTS = { contact: 30, name: 30, photo: 10, skills: 15, drivers: 15 } as const;
/** Career Drivers pays its own 100 XP (A-309), so the strength task carries no extra XP. */
const XP = { photo: 5, skills: 10, drivers: 0 } as const;

const SKILL_OPTIONS = [
  'Customer service',
  'Communication',
  'MS Excel',
  'Teamwork',
  'Problem solving',
  'Sales',
  'Hindi',
  'English',
  'Data entry',
  'Social media',
  'Time management',
  'Leadership',
];

const MOBILE = '+91 98765 40697';
const EMAIL = 'yash.pohare@thegamification.com';
const GENDER_LABEL: Record<Gender, string> = {
  female: 'Female',
  male: 'Male',
  'non-binary': 'Non-binary',
  'rather-not': 'Rather not say',
};

/** Kept for the tab like other mock stores, so the top bar shows it after a reload (A-277). */
const PHOTO_KEY = 'sm_mock_photo';
let photoUrl: string | null = (() => {
  try {
    return sessionStorage.getItem(PHOTO_KEY);
  } catch {
    return null;
  }
})();
export function mockPhoto(): string | null {
  return photoUrl;
}

function scenario(): string {
  return sceneOr('profile', 'new');
}

function initial(): State {
  return { skills: [], gender: 'rather-not', dob: '2000-08-14' };
}

function load(): State {
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? { ...initial(), ...(JSON.parse(raw) as Partial<State>) } : initial();
  } catch {
    return initial();
  }
}

function persist(s: State): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    /* private mode */
  }
}

function formatDob(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

function maskEmail(email: string | null): string | null {
  if (!email) return null;
  const [name = '', domain = ''] = email.split('@');
  return `${name.slice(0, 1)}••••@${domain}`;
}

function build(s: State, fresh: boolean) {
  const photo = photoUrl !== null;
  // One ledger everywhere (corrections #13 bugs): the profile shows the same total as every screen.
  const xp = fresh && !isNewLearner() ? 15 : totalXp();
  const lvl = levelFor(xp); // A-249: one ladder everywhere
  const level = lvl.level;
  const ca = assessmentSnapshot();
  const tasks = [
    { id: 'contact', label: 'Contact details', xp: 0, done: true },
    { id: 'name', label: 'Name & joining date', xp: 0, done: true },
    { id: 'photo', label: 'Add a profile photo', xp: XP.photo, done: photo },
    { id: 'skills', label: 'Add your top skills', xp: XP.skills, done: s.skills.length > 0 },
    { id: 'drivers', label: 'Take Career Drivers', xp: XP.drivers, done: !!ca?.done },
  ] as const;
  const percent = tasks.reduce((sum, t) => sum + (t.done ? WEIGHTS[t.id] : 0), 0);
  const year = s.dob.slice(0, 4);
  return {
    playerId: 'SM-00412',
    name: learnerName().full,
    initials: learnerName().initials,
    title: lvl.rank,
    joined: fresh ? '2026-10-05' : '2025-04-01',
    photoUrl,
    level,
    xp,
    levelStartXp: lvl.start,
    nextLevelXp: lvl.target,
    batchRank: fresh ? null : 12,
    bestSkill: fresh ? null : 'Digital Skills',
    // A-306: the archetype from the learner's own Career Drivers result.
    archetype: ca?.archetype
      ? {
          name: ca.archetype.name,
          blurb: ca.archetype.summary,
          emoji: ca.archetype.emoji,
          traits: ca.archetype.traits,
        }
      : null,
    // Corrections #24: quest badges count from day one.
    stats: fresh
      ? { streakDays: 0, badges: earnedBadges().length, learningHours: 0 }
      : { streakDays: 2, badges: earnedBadges().length, learningHours: 285.2 },
    strength: { percent, tasks },
    showcase:
      fresh && !earnedBadges().length
        ? { pinned: [], next: { name: 'First Steps', icon: 'star', percent: 40 } }
        : {
            pinned: showcase()
              .map((id) => badgeList().find((x) => x.id === id && x.state === 'earned'))
              .filter((x) => !!x)
              .map((x) => ({
                id: x.id,
                name: x.name,
                icon: x.icon,
                image: x.image ?? null,
                rarity: x.rarity,
              })),
            next:
              upNext(1).map((x) => ({
                name: x.name,
                icon: x.icon,
                image: x.image ?? null,
                percent: x.percent,
              }))[0] ?? null,
          },
    privateFields: [
      {
        key: 'mobile',
        label: 'Mobile number',
        masked: maskMobile(currentMobile(s)),
        empty: !currentMobile(s),
      },
      {
        key: 'email',
        label: 'Email',
        masked: maskEmail(currentEmail(s)) ?? 'y••••@thegamification.com',
        empty: false,
      },
      { key: 'gender', label: 'Gender', masked: 'Hidden', empty: false },
      {
        key: 'dob',
        label: 'Date of birth',
        masked: s.dob ? `•• ••• ${year}` : 'Not added',
        empty: !s.dob,
      },
    ],
    skills: s.skills,
  };
}

/** Private fields plus the DigiLocker-linked ID (masked, never revealable), A-272. */
function withId<T extends { privateFields: unknown[] }>(p: T): T {
  const id = linkedId();
  if (!id) return p;
  return {
    ...p,
    privateFields: [
      ...p.privateFields,
      {
        key: 'id',
        label: 'Government ID',
        masked: `${DOC_LABEL[id.doc] ?? 'ID'} ${id.masked}`,
        empty: true,
      },
    ],
  } as T;
}

export const profileHandlers = [
  http.get('/api/profile', async () => {
    await delay(400);
    if (scenario() === 'error') return new HttpResponse(null, { status: 503 });
    return HttpResponse.json(withId(build(load(), scenario() === 'new')));
  }),

  http.post('/api/profile/reveal', async ({ request }) => {
    await delay(250);
    const { field } = (await request.json()) as { field: string };
    const s = load();
    const value =
      field === 'mobile'
        ? currentMobile(s)
        : field === 'email'
          ? currentEmail(s)
          : field === 'gender'
            ? GENDER_LABEL[s.gender]
            : s.dob
              ? formatDob(s.dob)
              : '';
    return HttpResponse.json({ value });
  }),

  http.get('/api/profile/edit', async () => {
    await delay(300);
    const s = load();
    return HttpResponse.json({
      skills: s.skills,
      gender: s.gender,
      dob: s.dob,
      mobile: currentMobile(s),
      email: currentEmail(s),
      skillOptions: SKILL_OPTIONS,
    });
  }),

  // A-273: a new mobile or email needs a code sent to it (test code 123456).
  http.post('/api/profile/contact/code', async ({ request }) => {
    await delay(500);
    const { kind, value } = (await request.json()) as { kind: 'mobile' | 'email'; value: string };
    const ok =
      kind === 'mobile'
        ? /^\d{10}$/.test(value.replace(/\D/g, '').slice(-10))
        : /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value);
    if (!ok) return HttpResponse.json({ message: 'invalid' }, { status: 422 });
    pending[kind] = value;
    return HttpResponse.json({
      sentTo: kind === 'mobile' ? maskMobile(value) : (maskEmail(value) ?? value),
    });
  }),

  http.post('/api/profile/contact/verify', async ({ request }) => {
    await delay(500);
    const { kind, value, code } = (await request.json()) as {
      kind: 'mobile' | 'email';
      value: string;
      code: string;
    };
    if (pending[kind] !== value || code !== '123456')
      return HttpResponse.json({ message: 'wrong code' }, { status: 422 });
    Reflect.deleteProperty(pending, kind);
    persist({ ...load(), [kind]: value });
    rememberLogin(value);
    return new HttpResponse(null, { status: 204 });
  }),

  http.put('/api/profile', async ({ request }) => {
    await delay(600);
    const edit = (await request.json()) as { skills: string[]; gender: Gender; dob: string };
    const s = load();
    const firstSkills = !s.skills.length && edit.skills.length > 0;
    const next: State = {
      ...s,
      skills: edit.skills.slice(0, 5),
      gender: edit.gender,
      dob: edit.dob,
    };
    persist(next);
    if (firstSkills) creditXp(XP.skills);
    return HttpResponse.json({
      profile: withId(build(next, scenario() === 'new')),
      xpEarned: firstSkills ? XP.skills : 0,
    });
  }),

  http.post('/api/profile/photo', async ({ request }) => {
    await delay(900);
    const data = await request.formData();
    const file = data.get('photo');
    if (!(file instanceof File)) return new HttpResponse(null, { status: 400 });
    const first = photoUrl === null;
    photoUrl = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
    try {
      sessionStorage.setItem(PHOTO_KEY, photoUrl);
    } catch {
      /* too big or private mode: this page only */
    }
    if (first) creditXp(XP.photo);
    return HttpResponse.json({
      profile: withId(build(load(), scenario() === 'new')),
      xpEarned: first ? XP.photo : 0,
    });
  }),
];
```

## `src/mocks/programme.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { creditXp } from './xp-ledger';
import { mockProgress, syncLevel } from './quest.handlers';

/**
 * Programme code mocks. Valid codes: SKM-MUM-26-0417 (India) and SKM-JHB-26-0102 (South Africa).
 * 5 wrong tries → 429 (cool-down). IFSC HDFC0000240 → HDFC Bank · Andheri West; other well-formed codes
 * resolve to a generic branch; "XXXX0000000" → not found.
 */
let wrong = 0;

const PROGRAMMES: Record<string, object> = {
  'SKM-MUM-26-0417': {
    code: 'SKM-MUM-26-0417',
    name: 'Customer Support Associate',
    partner: 'NSQF Level 4 · Aarambh Foundation × Skill India',
    centre: 'Andheri West, Mumbai',
    batchStarts: '2026-10-12',
    duration: '3 months · 240 hrs',
    stipend: '₹5,000 / month',
    marksFrom: '12',
    minEnglish: 50,
    minMaths: 50,
    eligibility: 'Class 12 · 50%+ Eng & Maths',
  },
  'SKM-JHB-26-0102': {
    code: 'SKM-JHB-26-0102',
    name: 'Contact Centre Learnership',
    partner: 'NQF 4 · Ubuntu CX × Services SETA',
    centre: 'Randburg, Johannesburg',
    batchStarts: '2026-10-12',
    duration: '12 months',
    stipend: 'R6,500 / month',
    marksFrom: '12',
    minEnglish: 50,
    minMaths: 40,
    eligibility: 'Matric · 50%+ English, 40%+ Maths',
  },
};

export const programmeHandlers = [
  http.get('/api/programmes/:code', async ({ params }) => {
    await delay(700);
    if (wrong >= 5) return new HttpResponse(null, { status: 429 });
    const p = PROGRAMMES[String(params['code']).toUpperCase()];
    if (!p) {
      wrong += 1;
      return new HttpResponse(null, { status: 404 });
    }
    wrong = 0;
    return HttpResponse.json(p);
  }),

  http.get('/api/banks/ifsc/:code', async ({ params }) => {
    await delay(400);
    const code = String(params['code']).toUpperCase();
    if (code === 'HDFC0000240')
      return HttpResponse.json({ bank: 'HDFC Bank', branch: 'Andheri West, Mumbai' });
    if (code.startsWith('XXXX')) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json({ bank: `${code.slice(0, 4)} Bank`, branch: 'Main branch' });
  }),

  http.post('/api/me/programme', async ({ request }) => {
    await delay(900);
    const body = (await request.json()) as { bank: { account: string } | null };
    creditXp(100);
    syncLevel();
    return HttpResponse.json({
      xpEarned: 100,
      progress: mockProgress,
      accountTail: body.bank?.account.slice(-2) ?? '',
    });
  }),
];
```

## `src/mocks/quest.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { learnerName, onPersona, persona } from './persona';
import { QUEST_XP, STEP_XP, levelFor } from '../app/core/quests/levels';
import { totalXp } from './xp-ledger';

/** Mock quest progress for the signed-in learner (in memory, resets on reload of the service worker). */
const SLUGS = [
  'who-dis',
  'hit-me-up',
  'big-brain-stuff',
  'your-flex-era',
  'work-lore',
  'pick-your-arc',
  'drop-the-receipts',
  'vibe-check',
];
export const mockProgress = {
  completed: 0,
  partsDone: 0,
  xp: 0,
  badges: 0,
  level: 1,
  rank: 'Rookie',
  target: 400,
};
const answers: Record<string, Record<string, unknown>> = {
  'who-dis': { firstName: 'Yash' },
};

/** Survives a page refresh in this tab, like a real backend would (A-157). */
const KEY = 'sm_mock_quests';
try {
  const saved = JSON.parse(sessionStorage.getItem(KEY) ?? 'null') as {
    progress: typeof mockProgress;
    answers: typeof answers;
  } | null;
  if (saved) {
    Object.assign(mockProgress, saved.progress);
    Object.assign(answers, saved.answers);
  }
} catch {
  /* private mode or not a browser */
}
export function persistQuests(): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify({ progress: mockProgress, answers }));
  } catch {
    /* private mode: this page only */
  }
}

/**
 * A-286: the sample returning learner's answers, matching their Job Profile, so editing a section
 * from there opens it already filled in (the real backend returns the learner's own answers).
 */
const SAMPLE_ANSWERS: Readonly<Record<string, Record<string, unknown>>> = {
  'hit-me-up': {
    phone: '9876540697',
    phoneVerified: true,
    email: 'yash.pohare@thegamification.com',
    whatsapp: true,
    backupSkipped: true,
    postal: '411038',
    region: 'Maharashtra',
    district: 'Pune',
    city: 'Pune',
    area: 'Kothrud',
    line1: 'Flat 12, Shree Apartments, Paud Road',
    unit: '',
    landmark: 'Near Vanaz metro',
  },
  'big-brain-stuff': {
    level: 'graduate',
    status: 'completed',
    degree: 'B.Com',
    branch: 'Accounting & Finance',
    institute: 'Savitribai Phule Pune University',
    board: '',
    year: '2024',
    studyYear: '',
    scoreKind: 'cgpa',
    score: '7.4',
    timelineSkipped: true,
  },
  'your-flex-era': {
    skills: [
      {
        id: 'customer-service',
        name: 'Customer service',
        category: 'people',
        level: 3,
        source: 'cv',
      },
      {
        id: 'microsoft-excel',
        name: 'Microsoft Excel',
        category: 'business',
        level: 3,
        source: 'cv',
      },
      {
        id: 'verbal-communication',
        name: 'Verbal communication',
        category: 'people',
        level: 3,
        source: 'cv',
      },
      { id: 'data-entry', name: 'Data entry', category: 'data', level: 3, source: 'cv' },
      { id: 'zoho-crm', name: 'Zoho CRM', category: 'business', level: 2, source: 'cv' },
      { id: 'cold-calling', name: 'Cold calling', category: 'business', level: 2, source: 'cv' },
    ],
    languages: [
      {
        name: 'Hindi',
        fluency: 'native',
        speak: true,
        read: true,
        write: true,
        motherTongue: true,
        fromCv: false,
      },
      {
        name: 'English',
        fluency: 'fluent',
        speak: true,
        read: true,
        write: true,
        motherTongue: false,
        fromCv: true,
      },
      {
        name: 'Marathi',
        fluency: 'native',
        speak: true,
        read: true,
        write: false,
        motherTongue: false,
        fromCv: true,
      },
    ],
    trophiesSkipped: true,
  },
  'pick-your-arc': {
    lookingFor: ['full_time', 'internship'],
    roles: ['Customer support executive', 'BPO · voice process', 'Data entry operator'],
    where: ['near', 'remote'],
    shifts: ['day', 'rotational'],
    notice: 'immediate',
    salary: '3-4.5',
  },
};

/** Read-only view of a quest's saved answers for other mocks (job profile status, A-262). */
export function questAnswers(slug: string): Readonly<Record<string, unknown>> {
  return answers[slug] ?? {};
}

/** Lets other mocks pre-fill a quest (e.g. the CV parser fills Quest 4's deck and passport). */
export function seedAnswers(slug: string, values: Record<string, unknown>): void {
  answers[slug] = { ...values, ...answers[slug] };
  persistQuests();
}

/** Rookie 0 · Pathfinder 400 · Trailblazer 800 · Legend 1200 (mock rank ladder). */
export function syncLevel(): void {
  const l = levelFor(mockProgress.xp);
  Object.assign(mockProgress, { level: l.level, rank: l.rank, target: l.target });
  persistQuests();
}

/**
 * A-232: a new account starts the 8 quests from zero with its registered name. Logging back in
 * keeps the returning learner's own quest progress (they may have saved & exited mid-way, R1);
 * only a switch away from a new account restores the sample learner.
 */
const INITIAL = {
  completed: 0,
  partsDone: 0,
  xp: 0,
  badges: 0,
  level: 1,
  rank: 'Rookie',
  target: 400,
};
let last: string = persona();
onPersona((p) => {
  if (p === 'existing' && last !== 'new') {
    last = p;
    return;
  }
  last = p;
  for (const k of Object.keys(answers)) Reflect.deleteProperty(answers, k);
  Object.assign(mockProgress, INITIAL);
  answers['who-dis'] = { firstName: p === 'new' ? learnerName().first : 'Yash' };
  persistQuests();
});

export const questHandlers = [
  http.get('/api/me/progress', async () => {
    await delay(250);
    // One XP ledger: the learner's total (quests + assessment + learning + extras), not just quest XP.
    const xp = totalXp();
    const l = levelFor(xp);
    return HttpResponse.json({ ...mockProgress, xp, level: l.level, rank: l.rank, target: l.target });
  }),

  http.get('/api/me/quests/:slug/answers', async ({ params }) => {
    await delay(200);
    const slug = String(params['slug']);
    // Only when a section is opened for editing from the platform (the quests themselves start blank).
    const editing = typeof location !== 'undefined' && /[?&]edit=1\b/.test(location.search);
    const sample = editing && persona() === 'existing' ? (SAMPLE_ANSWERS[slug] ?? {}) : {};
    return HttpResponse.json({ ...sample, ...answers[slug] });
  }),

  http.put('/api/me/quests/:slug/parts/:part', async ({ params, request }) => {
    await delay(250);
    const slug = String(params['slug']);
    const { values, done, xp, bonusXp } = (await request.json()) as {
      values: Record<string, unknown>;
      done: boolean;
      xp?: boolean;
      bonusXp?: number;
    };
    const bonusBefore = Number(answers[slug]?.['bonusXp'] ?? 0);
    answers[slug] = { ...answers[slug], ...values };
    if (bonusXp !== undefined) void bonusBefore; // A-168: extras earn no XP
    if (done) {
      const part = Number(params['part']);
      // A-249: +20 the first time a step of the quest in progress is done (skips pass xp: false).
      const current = SLUGS.indexOf(slug) + 1 === mockProgress.completed + 1;
      if (current && xp !== false && part > mockProgress.partsDone) {
        mockProgress.xp += STEP_XP;
        syncLevel();
      }
      if (current) mockProgress.partsDone = Math.max(mockProgress.partsDone, part);
    }
    persistQuests();
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('/api/me/quests/:slug/complete', async ({ params }) => {
    await delay(400);
    const index = SLUGS.indexOf(String(params['slug']));
    const first = index + 1 > mockProgress.completed;
    if (first) {
      mockProgress.completed = index + 1;
      mockProgress.badges += 1;
      mockProgress.xp += QUEST_XP;
      mockProgress.partsDone = 0;
    }
    syncLevel();
    const xp = totalXp();
    const l = levelFor(xp);
    return HttpResponse.json({
      xpEarned: first ? QUEST_XP : 0,
      progress: { ...mockProgress, xp, level: l.level, rank: l.rank, target: l.target },
    });
  }),
];
```

## `src/mocks/stats.handlers.ts`

```ts
import { HttpResponse, delay, http } from 'msw';
import { totalXp } from './xp-ledger';
import { isNewLearner, learnerName, sceneOr } from './persona';
import { levelFor } from '../app/core/quests/levels';
import { badgeList, earnedBadges, setShowcase, showcase, upNext } from './badges.data';

/**
 * My Stats (Figma Dashboard v2 161:4277). Sample data from the design.
 * `?stats=error` fails every slide; `?stats=empty` shows the Assessments empty state (161:4514) and
 * a learner with no courses started.
 */

function scenario(): string {
  return sceneOr('stats', 'empty');
}

function nextMidnight(): string {
  const d = new Date();
  d.setHours(24, 0, 0, 0);
  return d.toISOString();
}

function catalogue() {
  const pinned = showcase();
  return {
    earned: earnedBadges().length,
    total: badgeList().length,
    badges: badgeList().map((x) => ({ ...x, pinned: pinned.includes(x.id) })),
  };
}

function rewards() {
  const pinned = showcase()
    .map((id) => badgeList().find((x) => x.id === id && x.state === 'earned'))
    .filter((x) => !!x)
    .map((x) => ({
      id: x.id,
      name: x.name,
      icon: x.icon,
      image: x.image ?? null,
      rarity: x.rarity,
    }));
  const slots = [...pinned, null, null, null].slice(0, 3);
  // A new learner (A-232): only the registration-quest XP, day 1 of a streak, nothing earned yet.
  const fresh = isNewLearner();
  const me = learnerName();
  const xp = totalXp();
  return {
    player: {
      name: me.full,
      initials: me.initials,
      title: levelFor(xp).rank,
      photoUrl: null,
      level: levelFor(xp).level,
      xp,
      nextLevelXp: levelFor(xp).target,
      batchRank: fresh ? null : 12,
    },
    streak: {
      days: fresh ? 1 : 2,
      week: fresh
        ? ['upcoming', 'upcoming', 'today', 'upcoming', 'upcoming', 'upcoming', 'upcoming']
        : ['done', 'done', 'today', 'upcoming', 'upcoming', 'upcoming', 'upcoming'],
    },
    wallet: { xp, coins: 0, coinsEarned: 0, coinsSpent: 0, credits: 0 },
    quests: {
      resetsAt: nextMidnight(),
      items: [
        {
          id: 'finish-bv',
          icon: 'play',
          title: 'Finish Brain-Vita',
          progressLabel: fresh ? '0% done' : '98.86% done',
          percent: fresh ? 0 : 98.86,
          xp: 10,
          done: false,
          link: '/learn/brain-vita',
        },
        {
          id: 'learn-15',
          icon: 'clock',
          title: 'Learn for 15 minutes',
          progressLabel: fresh ? '0 / 15 min' : '6 / 15 min',
          percent: fresh ? 0 : 40,
          xp: 5,
          done: false,
          link: '/home',
        },
        {
          id: 'quiz-80',
          icon: 'lightbulb',
          title: 'Score 80%+ on any quiz',
          progressLabel: '0 / 1',
          percent: 0,
          xp: 5,
          done: false,
          link: '/learn/brain-vita/non-verbal-reasoning/quiz',
        },
      ],
    },
    badges: {
      earned: earnedBadges().length,
      total: badgeList().length,
      showcase: slots,
      upNext: upNext(3).map((x) => ({
        id: x.id,
        name: x.name,
        icon: x.icon,
        image: x.image ?? null,
        progressLabel: x.progressLabel ?? '',
        percent: x.percent,
      })),
    },
    rank: fresh
      ? { rank: 48, gapXp: 15, nextName: 'Riya' }
      : { rank: 12, gapXp: 48, nextName: 'Aarav' },
    learningHours: fresh ? 0 : 285.2,
  };
}

const C = (
  id: string,
  title: string,
  tone: string,
  percent: number,
  status: string,
  link: string,
  summary: string,
  deadlineDays: number | null = null,
) => ({
  id,
  title,
  tone,
  percent,
  status,
  kind: 'Standard',
  xpOnFinish: 50,
  deadlineDays,
  timeEstHours: null,
  cpdHours: null,
  summary,
  link,
});

const NOT_STARTED = [
  'Retail Basics',
  'Healthcare Support',
  'Logistics 101',
  'Customer Service Pro',
  'Spoken English',
  'Financial Literacy',
  'Digital Marketing',
  'Workplace Safety',
  'Excel Essentials',
  'Teamwork at Work',
  'Interview Ready',
  'Time Management',
  'Sales Starter',
  'Data Entry',
  'Electrician Basics',
  'Hospitality Advanced',
  'Tally for Beginners',
];

function learning() {
  if (scenario() === 'empty') {
    return {
      courses: NOT_STARTED.slice(0, 5).map((t, i) =>
        C(
          `ns-${i}`,
          t,
          'blue',
          0,
          'not-started',
          '/home',
          `Start ${t} to earn XP and a certificate.`,
        ),
      ),
      currentId: null,
      nudge: null,
    };
  }
  const tones = ['arcane', 'ember', 'green', 'amber', 'pink', 'blue'];
  return {
    courses: [
      C(
        'brain-vita',
        'Brain-Vita',
        'arcane',
        98.86,
        'in-progress',
        '/learn/brain-vita',
        'Sharpen verbal, non-verbal, arithmetic and data reasoning.',
        6,
      ),
      C(
        'hospitality',
        'Hospitality',
        'ember',
        56.67,
        'in-progress',
        '/home',
        'Front desk, guest care and service skills for hotels and restaurants.',
        14,
      ),
      C(
        'it-gyan',
        'IT Gyan',
        'green',
        37.78,
        'in-progress',
        '/home',
        'Computer basics, the internet and staying safe online.',
        21,
      ),
      C(
        'banking',
        'Banking',
        'amber',
        8.7,
        'in-progress',
        '/home',
        'Accounts, payments and the jobs that keep a bank running.',
        30,
      ),
      C(
        'intl-opps',
        'International Opportunities',
        'arcane',
        0.25,
        'in-progress',
        '/home',
        'Discover global career opportunities for India’s aspiring blue-collar workforce: plan your skill development, navigate overseas employment and build a sustainable future.',
        0,
      ),
      C(
        'auto-components',
        'Auto Components',
        'pink',
        0.25,
        'in-progress',
        '/home',
        'How vehicle parts are made, checked and fitted.',
        45,
      ),
      ...NOT_STARTED.map((t, i) =>
        C(
          `ns-${i}`,
          t,
          tones[i % tones.length] ?? 'blue',
          0,
          'not-started',
          '/home',
          `Start ${t} to earn XP and a certificate.`,
        ),
      ),
      C('first-steps', 'Skillmuni Basics', 'green', 100, 'done', '/home', 'How Skillmuni works.'),
    ],
    currentId: 'intl-opps',
    nudge: 'Brain-Vita is 98.86% done. One more push for the badge!',
  };
}

const A = (
  id: string,
  title: string,
  tier: string,
  score: number,
  correct: number,
  completed: string,
) => ({
  id,
  title,
  subtitle: `${title} Mastery`,
  tier,
  score,
  correct,
  total: 20,
  completed,
  retakeUrl: `/assessments/${id}`,
});

function assessments() {
  const recommended = {
    title: 'Digital Skills',
    url: '/assessments/digital-skills',
    why: 'Start with Digital Skills. Most of your batch unlocks it first.',
  };
  if (scenario() === 'empty') return { results: [], available: 20, firstCardXp: 20, recommended };
  return {
    results: [
      A('digital-skills', 'Digital Skills', 'expert', 95, 18, '2026-06-07'),
      A('leadership', 'Leadership & Ownership', 'expert', 94, 18, '2026-06-02'),
      A('risk', 'Risk & Adaptability', 'advanced', 86, 17, '2026-05-28'),
      A('ai', 'AI & Emerging Technologies', 'advanced', 81, 16, '2026-05-20'),
      A('tech-innovation', 'Technology Innovation', 'intermediate', 75, 15, '2026-05-14'),
      A('data-analytics', 'Data Analytics', 'intermediate', 71, 14, '2026-05-09'),
      A('entrepreneurial', 'Entrepreneurial Mindset', 'beginner', 59, 12, '2026-05-02'),
      A('sales', 'Sales & Growth', 'beginner', 58, 12, '2026-04-25'),
      A('cyber', 'Cyber Awareness', 'beginner', 57, 11, '2026-04-18'),
      A('startup', 'Startup Readiness', 'beginner', 42, 8, '2026-04-11'),
      A('communication', 'Communication', 'beginner', 3, 1, '2026-04-04'),
    ],
    available: 20,
    firstCardXp: 20,
    recommended,
  };
}

const fail = () => scenario() === 'error';

export const statsHandlers = [
  http.get('/api/stats/rewards', async () => {
    await delay(400);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(rewards());
  }),
  http.get('/api/stats/learning', async () => {
    await delay(400);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(learning());
  }),
  http.get('/api/stats/assessments', async () => {
    await delay(400);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(assessments());
  }),
  http.get('/api/badges', async () => {
    await delay(400);
    return fail() ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(catalogue());
  }),
  http.put('/api/badges/showcase', async ({ request }) => {
    await delay(300);
    const { ids } = (await request.json()) as { ids: string[] };
    const earned = new Set(earnedBadges().map((x) => x.id));
    setShowcase(ids.filter((id) => earned.has(id)).slice(0, 3));
    return HttpResponse.json(catalogue());
  }),
];
```

## `src/mocks/xp-ledger.ts`

```ts
import { isNewLearner } from './persona';
import { mockProgress, syncLevel } from './quest.handlers';

/**
 * One XP total for the whole mock (mirrors the backend's single XP ledger, MISS-01).
 * Every screen — quest HUD, Profile, My Stats, Arena, Muni — reads `totalXp()`, and every reward
 * goes through `creditXp()`, so the learner never sees two different totals or rank names.
 *   - New account: the quest progress XP is the ledger.
 *   - The sample returning learner: 529 XP of history, plus anything earned in this tab.
 */
export const SAMPLE_XP = 529;
const EXTRA = 'sm_mock_xp_extra';

function extra(): number {
  try {
    return Number(sessionStorage.getItem(EXTRA) ?? 0) || 0;
  } catch {
    return 0;
  }
}

export function totalXp(): number {
  return isNewLearner() ? mockProgress.xp : SAMPLE_XP + extra();
}

export function creditXp(amount: number): void {
  if (amount <= 0) return;
  if (isNewLearner()) {
    mockProgress.xp += amount;
    syncLevel();
    return;
  }
  try {
    sessionStorage.setItem(EXTRA, String(extra() + amount));
  } catch {
    /* private mode */
  }
}
```
