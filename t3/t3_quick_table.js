// Check minimum depth before having to brute-force for Quick

// To use, just copy&paste into a browser dev console

const MODE = 0 // 0 = normal, 1 = checked, 2 = inverted checked

// Player 1 wins
const patternWin1 = [
  // horizontal
  0b010101_000000_000000,
  0b000000_010101_000000,
  0b000000_000000_010101,
  // vertical
  0b010000_010000_010000,
  0b000100_000100_000100,
  0b000001_000001_000001,
  // diagonals
  0b010000_000100_000001,
  0b000001_000100_010000
]
// Player 2 wins
const patternWin2 = [
  // horizontal
  0b101010_000000_000000,
  0b000000_101010_000000,
  0b000000_000000_101010,
  // vertical
  0b100000_100000_100000,
  0b001000_001000_001000,
  0b000010_000010_000010,
  // diagonals
  0b100000_001000_000010,
  0b000010_001000_100000
]

const patternNearWinMask = [ // Which squares will be checked?
  // horizontal
  0b111111_000000_000000,
  0b111111_000000_000000,
  0b111111_000000_000000,
  0b000000_111111_000000,
  0b000000_111111_000000,
  0b000000_111111_000000,
  0b000000_000000_111111,
  0b000000_000000_111111,
  0b000000_000000_111111,
  // vertical
  0b110000_110000_110000,
  0b110000_110000_110000,
  0b110000_110000_110000,
  0b001100_001100_001100,
  0b001100_001100_001100,
  0b001100_001100_001100,
  0b000011_000011_000011,
  0b000011_000011_000011,
  0b000011_000011_000011,
  // diagonals
  0b110000_001100_000011,
  0b110000_001100_000011,
  0b110000_001100_000011,
  0b000011_001100_110000,
  0b000011_001100_110000,
  0b000011_001100_110000,
]

const patternNearWin1 = [ // Player 1 can win on the next move
  // horizontal
  0b000101_000000_000000,
  0b010001_000000_000000,
  0b010100_000000_000000,
  0b000000_000101_000000,
  0b000000_010001_000000,
  0b000000_010100_000000,
  0b000000_000000_000101,
  0b000000_000000_010001,
  0b000000_000000_010100,
  // vertical
  0b000000_010000_010000,
  0b010000_000000_010000,
  0b010000_010000_000000,
  0b000000_000100_000100,
  0b000100_000000_000100,
  0b000100_000100_000000,
  0b000000_000001_000001,
  0b000001_000000_000001,
  0b000001_000001_000000,
  // diagonals
  0b000000_000100_000001,
  0b010000_000000_000001,
  0b010000_000100_000000,
  0b000000_000100_010000,
  0b000001_000000_010000,
  0b000001_000100_000000,
]

const patternNearWin2 = [ // Player 2 can win on the next move
  // horizontal
  0b001010_000000_000000,
  0b100010_000000_000000,
  0b101000_000000_000000,
  0b000000_001010_000000,
  0b000000_100010_000000,
  0b000000_101000_000000,
  0b000000_000000_001010,
  0b000000_000000_100010,
  0b000000_000000_101000,
  // vertical
  0b000000_100000_100000,
  0b100000_000000_100000,
  0b100000_100000_000000,
  0b000000_001000_001000,
  0b001000_000000_001000,
  0b001000_001000_000000,
  0b000000_000010_000010,
  0b000010_000000_000010,
  0b000010_000010_000000,
  // diagonals
  0b000000_001000_000010,
  0b100000_000000_000010,
  0b100000_001000_000000,
  0b000000_001000_100000,
  0b000010_000000_100000,
  0b000010_001000_000000,
]


function occupied (state, spot) {
  const spaceWeWant = 3 << (spot << 1)
  return state & spaceWeWant
}

function checkWin (state, depth) {
  if (isWin(state, !(depth & 1))) {
    return (depth & 1) ? 1 : 2
  }
  return depth === 9 ? 3 : 0
}

function isWin (state, p2) {
  return checkPatterns(state, p2 ? patternWin2 : patternWin1)
}

function isNearWin (state, p2) {
  return checkPatterns(state, p2 ? patternNearWin2 : patternNearWin1, patternNearWinMask)
}

function checkPatterns (board, patterns, masks = patterns) {
  return patterns.some((pattern, index) => pattern === (board & masks[index]))
}

function validMove (state, i, stateNext, depth) {
  if (occupied(state, i)) return false

  if (MODE === 1 && isNearWin(stateNext, !(depth & 1))) return false
  else if (MODE === 2 && isWin(stateNext, depth & 1)) return false

  return true
}

const memoEndFlags = {}

function buildTables (state, depth, mark, memo) {
  if (memo[state]) return memo[state]

  const win = checkWin(state, depth)

  if (win) {
    // Terminal state
    return memo[state] = [1 << (win - 1), true]
  }

  const markNext = mark ^ 3
  const depthNext = depth + 1

  let endFlags = 0
  for (let i = 0; i < 9; ++i) {
    const stateNext = state | (mark << (i << 1))
    if (validMove(state, i, stateNext, depth)) {
      endFlags |= buildTables(stateNext, depthNext, markNext, memo)[0]

      // no early exit (must visit all states)
      // if (endFlags === 3) break
    }
  }

  return memo[state] = endFlags ? [endFlags, false] : [((depthNext & 1) ^ (MODE - 1)) + 1, true]
}

console.log(buildTables(0, 0, 1, memoEndFlags))

function browse (state, depth, mark, memo, log, vis) {
  vis.add(state)

  const markNext = mark ^ 3
  const depthNext = depth + 1

  const moves = [0, 1, 2, 3, 4, 5, 6, 7, 8]
    .map((i) => {
      const stateNext = state | (mark << (i << 1))
      if (!validMove(state, i, stateNext, depth)) return

      const [result, terminal] = memo[stateNext]

      if (!(terminal || vis.has(stateNext))) browse(stateNext, depthNext, markNext, memo, log, vis)

      return [result, stateNext, depthNext, terminal]
    })
    .filter(x => x)

  if (!moves.every((r) => r === moves[0])) {
    moves.forEach(([v, stateNext, depthNext, terminal]) => {
      if ((v === 1 || v === 2 || v === 4) && !terminal) {
        // log[v] = Math.min(depthNext, log[v] ?? Number.POSITIVE_INFINITY)
        // (log[v] ??= {})[stateNext] = v
        log[stateNext] = v == 4 ? 3 : v
      }
    })
  }

  return log
}

window.result = browse(0, 0, 1, memoEndFlags, {}, new Set())

// console.log(window.result)
const states = Object.keys(window.result).sort((a, b) => a - b)
console.log(states.join(','))
console.log(states.map(k => window.result[k]).join(','))

// min depths for (X, O, draw) by MODE
// 0: 5,6,6
// 1: 3,3,3
// 2: 6,4,6
