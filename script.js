// ==========================================
// 0. 공통 UI (탭 전환, 테마, 토스트 알림)
// ==========================================
const tabButtons = document.querySelectorAll('.tab-btn');
const tabPanels = document.querySelectorAll('.tab-panel');
const themeToggleBtn = document.getElementById('themeToggleBtn');
const toastEl = document.getElementById('toast');

function showToast(msg) {
  toastEl.textContent = msg;
  toastEl.classList.add('show');
  setTimeout(() => toastEl.classList.remove('show'), 1800);
}

tabButtons.forEach((btn) => {
  btn.addEventListener('click', () => {
    const target = btn.dataset.tab;
    tabButtons.forEach((b) => b.classList.remove('active'));
    tabPanels.forEach((p) => p.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById(`tab-${target}`).classList.add('active');
  });
});

themeToggleBtn.addEventListener('click', () => {
  document.body.classList.toggle('light-theme');
  const isLight = document.body.classList.contains('light-theme');
  themeToggleBtn.textContent = isLight ? '☀️' : '🌙';
});

// ==========================================
// 1. 계산기 (일반 + 공학용 + 기록)
// ==========================================
const calcExpressionEl = document.getElementById('calcExpression');
const calcDisplayEl = document.getElementById('calcDisplay');
const historyListEl = document.getElementById('historyList');
const sciModeToggle = document.getElementById('sciModeToggle');
const sciKeypad = document.getElementById('sciKeypad');
const angleModeWrap = document.getElementById('angleModeWrap');
const degRadBtn = document.getElementById('degRadBtn');

let currentInput = '0';
let expression = '';
let isDegree = true;
let justEvaluated = false;
let calcHistory = JSON.parse(localStorage.getItem('smart_calc_history') || '[]');

sciModeToggle.addEventListener('change', () => {
  const show = sciModeToggle.checked;
  sciKeypad.style.display = show ? 'grid' : 'none';
  angleModeWrap.style.display = show ? 'block' : 'none';
});

degRadBtn.addEventListener('click', () => {
  isDegree = !isDegree;
  degRadBtn.textContent = isDegree ? 'DEG' : 'RAD';
});

function formatNumberDisplay(val) {
  if (val === '오류' || val === 'Infinity' || val === '-Infinity' || val === 'NaN') {
    return '오류';
  }
  const str = String(val);
  if (str.includes('e') || str.includes('E')) return str;
  const parts = str.split('.');
  const intPart = parts[0];
  const decPart = parts.length > 1 ? '.' + parts[1] : '';
  const sign = intPart.startsWith('-') ? '-' : '';
  const cleanInt = intPart.replace('-', '');
  if (!/^\d+$/.test(cleanInt)) return str;
  return sign + cleanInt.replace(/\B(?=(\d{3})+(?!\d))/g, ',') + decPart;
}

function updateCalcScreen() {
  calcExpressionEl.textContent = expression
    .replace(/\*/g, '×')
    .replace(/\//g, '÷')
    .replace(/-/g, '−');
  calcDisplayEl.textContent = formatNumberDisplay(currentInput);
}

function appendValue(val) {
  if (justEvaluated) {
    if (/[0-9.]/.test(val)) {
      expression = '';
      currentInput = val === '.' ? '0.' : val;
    } else {
      expression = currentInput + val;
      currentInput = '0';
    }
    justEvaluated = false;
    updateCalcScreen();
    return;
  }

  if (/[0-9]/.test(val)) {
    if (currentInput === '0' || currentInput === '오류') {
      currentInput = val === '00' || val === '000' ? '0' : val;
    } else {
      currentInput += val;
    }
  } else if (val === '.') {
    if (!currentInput.includes('.')) {
      currentInput += '.';
    }
  } else if (['+', '-', '*', '/'].includes(val)) {
    if (expression && /[+\-*/]$/.test(expression) && currentInput === '0') {
      expression = expression.slice(0, -1) + val;
    } else {
      expression += currentInput + val;
      currentInput = '0';
    }
  }
  updateCalcScreen();
}

function factorial(n) {
  if (n < 0 || !Number.isInteger(n)) return NaN;
  if (n > 170) return Infinity;
  let r = 1;
  for (let i = 2; i <= n; i++) r *= i;
  return r;
}

function applySciFunc(type) {
  let x = parseFloat(currentInput);
  if (isNaN(x)) return;
  let res = x;
  const rad = isDegree ? (x * Math.PI) / 180 : x;

  switch (type) {
    case 'sin':
      res = Math.sin(rad);
      break;
    case 'cos':
      res = Math.cos(rad);
      break;
    case 'tan':
      res = Math.tan(rad);
      break;
    case 'sqrt':
      res = Math.sqrt(x);
      break;
    case 'sq':
      res = x * x;
      break;
    case 'pow':
      expression += currentInput + '**';
      currentInput = '0';
      justEvaluated = false;
      updateCalcScreen();
      return;
    case 'log':
      res = Math.log10(x);
      break;
    case 'ln':
      res = Math.log(x);
      break;
    case 'pi':
      res = Math.PI;
      break;
    case 'e':
      res = Math.E;
      break;
    case 'inv':
      res = 1 / x;
      break;
    case 'fact':
      res = factorial(x);
      break;
  }

  const rounded = Number(res.toPrecision(12));
  currentInput = isFinite(rounded) ? String(rounded) : '오류';
  justEvaluated = true;
  updateCalcScreen();
}

function evaluateCalc() {
  let fullExpr = expression + currentInput;
  if (!fullExpr) return;

  // 괄호 자동 닫기
  const openCount = (fullExpr.match(/\(/g) || []).length;
  const closeCount = (fullExpr.match(/\)/g) || []).length;
  if (openCount > closeCount) {
    fullExpr += ')'.repeat(openCount - closeCount);
  }

  try {
    // 안전한 수식 검증
    if (!/^[0-9+\-*/().\s*]+$/.test(fullExpr)) {
      throw new Error('Invalid');
    }
    // eslint-disable-next-line no-new-func
    const rawResult = Function(`"use strict"; return (${fullExpr})`)();
    if (!isFinite(rawResult) || isNaN(rawResult)) {
      currentInput = '오류';
    } else {
      const cleanResult = Number(rawResult.toPrecision(12));
      const prettyExpr = fullExpr
        .replace(/\*\*/g, '^')
        .replace(/\*/g, '×')
        .replace(/\//g, '÷');
      addCalcHistory(prettyExpr, String(cleanResult));
      expression = prettyExpr + ' =';
      currentInput = String(cleanResult);
      justEvaluated = true;
    }
  } catch (e) {
    currentInput = '오류';
  }
  updateCalcScreen();
}

function handleCalcAction(action) {
  if (action === 'clear') {
    currentInput = '0';
    expression = '';
    justEvaluated = false;
  } else if (action === 'backspace') {
    if (justEvaluated || currentInput === '오류') {
      currentInput = '0';
      justEvaluated = false;
    } else if (currentInput.length > 1) {
      currentInput = currentInput.slice(0, -1);
    } else {
      currentInput = '0';
    }
  } else if (action === 'sign') {
    if (currentInput !== '0' && currentInput !== '오류') {
      currentInput = currentInput.startsWith('-')
        ? currentInput.slice(1)
        : '-' + currentInput;
    }
  } else if (action === 'percent') {
    const num = parseFloat(currentInput);
    if (!isNaN(num)) {
      currentInput = String(Number((num / 100).toPrecision(12)));
    }
  } else if (action === 'paren') {
    const openCount = (expression.match(/\(/g) || []).length;
    const closeCount = (expression.match(/\)/g) || []).length;
    if (openCount === closeCount || /[+\-*/(]$/.test(expression)) {
      if (currentInput !== '0') {
        expression += currentInput + '*(';
        currentInput = '0';
      } else {
        expression += '(';
      }
    } else {
      expression += currentInput + ')';
      currentInput = '0';
    }
  } else if (action === 'equals') {
    evaluateCalc();
    return;
  }
  updateCalcScreen();
}

document.querySelectorAll('.calc-grid .key, .calc-extra-row .key').forEach((btn) => {
  btn.addEventListener('click', () => {
    if (btn.dataset.val) appendValue(btn.dataset.val);
    if (btn.dataset.action) handleCalcAction(btn.dataset.action);
  });
});

document.querySelectorAll('.sci-grid .key').forEach((btn) => {
  btn.addEventListener('click', () => applySciFunc(btn.dataset.sci));
});

document.getElementById('copyCalcResult').addEventListener('click', () => {
  navigator.clipboard.writeText(currentInput);
  showToast(`결과(${formatNumberDisplay(currentInput)})가 복사되었습니다!`);
});

// 키보드 입력 지원
window.addEventListener('keydown', (e) => {
  const calcTabActive = document.getElementById('tab-calculator').classList.contains('active');
  if (!calcTabActive) return;
  if (['INPUT', 'SELECT', 'TEXTAREA'].includes(e.target.tagName)) return;

  if (e.key >= '0' && e.key <= '9') {
    appendValue(e.key);
  } else if (e.key === '.') {
    appendValue('.');
  } else if (['+', '-', '*', '/'].includes(e.key)) {
    appendValue(e.key);
  } else if (e.key === 'Enter' || e.key === '=') {
    e.preventDefault();
    evaluateCalc();
  } else if (e.key === 'Backspace') {
    handleCalcAction('backspace');
  } else if (e.key === 'Escape') {
    handleCalcAction('clear');
  } else if (e.key === '%') {
    handleCalcAction('percent');
  }
});

// 계산 기록 렌더링 & 관리
function renderCalcHistory() {
  if (!calcHistory.length) {
    historyListEl.innerHTML = '<li class="empty-msg">아직 계산 기록이 없습니다.</li>';
    return;
  }
  historyListEl.innerHTML = calcHistory
    .map(
      (item, idx) => `
      <li class="history-item" data-idx="${idx}">
        <div class="h-expr">${item.expr}</div>
        <div class="h-res">${formatNumberDisplay(item.res)}</div>
      </li>
    `
    )
    .join('');

  historyListEl.querySelectorAll('.history-item').forEach((li) => {
    li.addEventListener('click', () => {
      const item = calcHistory[Number(li.dataset.idx)];
      currentInput = item.res;
      expression = '';
      justEvaluated = true;
      updateCalcScreen();
      showToast('기록에서 값을 불러왔습니다.');
    });
  });
}

function addCalcHistory(expr, res) {
  calcHistory.unshift({ expr, res });
  if (calcHistory.length > 30) calcHistory.pop();
  localStorage.setItem('smart_calc_history', JSON.stringify(calcHistory));
  renderCalcHistory();
}

document.getElementById('clearHistoryBtn').addEventListener('click', () => {
  calcHistory = [];
  localStorage.removeItem('smart_calc_history');
  renderCalcHistory();
});

renderCalcHistory();
updateCalcScreen();

// ==========================================
// 2. 실시간 환율 조회 및 계산기
// ==========================================
const CURRENCY_INFO = {
  USD: { name: '미국 달러', symbol: '$', flag: '🇺🇸' },
  KRW: { name: '대한민국 원', symbol: '₩', flag: '🇰🇷' },
  JPY: { name: '일본 엔', symbol: '¥', flag: '🇯🇵' },
  EUR: { name: '유럽연합 유로', symbol: '€', flag: '🇪🇺' },
  CNY: { name: '중국 위안', symbol: '¥', flag: '🇨🇳' },
  GBP: { name: '영국 파운드', symbol: '£', flag: '🇬🇧' },
  AUD: { name: '호주 달러', symbol: 'A$', flag: '🇦🇺' },
  CAD: { name: '캐나다 달러', symbol: 'C$', flag: '🇨🇦' },
  CHF: { name: '스위스 프랑', symbol: 'CHF', flag: '🇨🇭' },
  HKD: { name: '홍콩 달러', symbol: 'HK$', flag: '🇭🇰' },
  SGD: { name: '싱가포르 달러', symbol: 'S$', flag: '🇸🇬' },
  TWD: { name: '대만 달러', symbol: 'NT$', flag: '🇹🇼' },
  THB: { name: '태국 바트', symbol: '฿', flag: '🇹🇭' },
  VND: { name: '베트남 동', symbol: '₫', flag: '🇻🇳' },
  PHP: { name: '필리핀 페소', symbol: '₱', flag: '🇵🇭' },
  MYR: { name: '말레이시아 링깃', symbol: 'RM', flag: '🇲🇾' }
};

// 오프라인/기본 폴백 환율 (1 USD 기준)
let exchangeRates = {
  USD: 1,
  KRW: 1385.5,
  JPY: 153.2,
  EUR: 0.924,
  CNY: 7.24,
  GBP: 0.772,
  AUD: 1.51,
  CAD: 1.388,
  CHF: 0.866,
  HKD: 7.77,
  SGD: 1.322,
  TWD: 32.08,
  THB: 33.75,
  VND: 25280,
  PHP: 58.1,
  MYR: 4.35
};

const fromCurrencySel = document.getElementById('fromCurrency');
const toCurrencySel = document.getElementById('toCurrency');
const fromAmountInput = document.getElementById('fromCurrencyAmount');
const toAmountInput = document.getElementById('toCurrencyAmount');
const fromCurrencySub = document.getElementById('fromCurrencySub');
const toCurrencySub = document.getElementById('toCurrencySub');
const rateStatusBadge = document.getElementById('rateStatusBadge');
const rateUpdateTime = document.getElementById('rateUpdateTime');
const exchangeSummaryBar = document.getElementById('exchangeSummaryBar');
const popularRatesGrid = document.getElementById('popularRatesGrid');

function populateCurrencySelects() {
  const optionsHtml = Object.entries(CURRENCY_INFO)
    .map(([code, info]) => `<option value="${code}">${info.flag} ${code} - ${info.name}</option>`)
    .join('');
  fromCurrencySel.innerHTML = optionsHtml;
  toCurrencySel.innerHTML = optionsHtml;
  fromCurrencySel.value = 'USD';
  toCurrencySel.value = 'KRW';
}

function formatKoreanMoneyHint(num, code) {
  if (!isFinite(num) || num === 0) return '';
  const abs = Math.abs(num);
  const info = CURRENCY_INFO[code];
  if (abs >= 10000) {
    const eok = Math.floor(abs / 100000000);
    const man = Math.floor((abs % 100000000) / 10000);
    const rest = Math.round(abs % 10000);
    let parts = [];
    if (eok > 0) parts.push(`${eok.toLocaleString()}억`);
    if (man > 0) parts.push(`${man.toLocaleString()}만`);
    if (rest > 0) parts.push(`${rest.toLocaleString()}`);
    return `약 ${parts.join(' ')} ${info.name}`;
  }
  return `${num.toLocaleString(undefined, { maximumFractionDigits: 4 })} ${info.name}`;
}

function convertCurrency(direction = 'forward') {
  const fromCode = fromCurrencySel.value;
  const toCode = toCurrencySel.value;
  const rateFrom = exchangeRates[fromCode] || 1;
  const rateTo = exchangeRates[toCode] || 1;

  if (direction === 'forward') {
    const val = parseFloat(fromAmountInput.value);
    if (isNaN(val)) {
      toAmountInput.value = '';
      return;
    }
    const result = (val / rateFrom) * rateTo;
    const decimals = ['KRW', 'JPY', 'VND'].includes(toCode) ? 2 : 4;
    toAmountInput.value = Number(result.toFixed(decimals));
  } else {
    const val = parseFloat(toAmountInput.value);
    if (isNaN(val)) {
      fromAmountInput.value = '';
      return;
    }
    const result = (val / rateTo) * rateFrom;
    const decimals = ['KRW', 'JPY', 'VND'].includes(fromCode) ? 2 : 4;
    fromAmountInput.value = Number(result.toFixed(decimals));
  }

  const fVal = parseFloat(fromAmountInput.value) || 0;
  const tVal = parseFloat(toAmountInput.value) || 0;
  fromCurrencySub.textContent = formatKoreanMoneyHint(fVal, fromCode);
  toCurrencySub.textContent = formatKoreanMoneyHint(tVal, toCode);

  const unitRate = (1 / rateFrom) * rateTo;
  const jpyUnitRate = (100 / exchangeRates.JPY) * exchangeRates.KRW;
  let extra = '';
  if (fromCode === 'JPY' && toCode === 'KRW') {
    extra = ` (100 JPY = ${jpyUnitRate.toLocaleString(undefined, { maximumFractionDigits: 2 })} KRW)`;
  }
  exchangeSummaryBar.textContent = `1 ${fromCode} = ${unitRate.toLocaleString(undefined, {
    maximumFractionDigits: 4
  })} ${toCode}${extra}`;

  renderPopularRatesGrid();
}

function renderPopularRatesGrid() {
  const fromCode = fromCurrencySel.value;
  const amount = parseFloat(fromAmountInput.value) || 1;
  const krwRate = exchangeRates.KRW;

  const codes = ['USD', 'KRW', 'JPY', 'EUR', 'CNY', 'GBP', 'AUD', 'CAD', 'VND', 'THB', 'TWD', 'SGD'];
  popularRatesGrid.innerHTML = codes
    .map((code) => {
      const info = CURRENCY_INFO[code];
      const converted = (amount / exchangeRates[fromCode]) * exchangeRates[code];
      const perKrw =
        code === 'JPY'
          ? `100 ${code} = ${((100 / exchangeRates[code]) * krwRate).toFixed(1)} 원`
          : code === 'VND'
          ? `100 ${code} = ${((100 / exchangeRates[code]) * krwRate).toFixed(2)} 원`
          : code === 'KRW'
          ? `기준 통화 (대한민국 원)`
          : `1 ${code} = ${((1 / exchangeRates[code]) * krwRate).toFixed(2)} 원`;

      return `
        <div class="rate-card" data-code="${code}">
          <div class="rate-card-top">
            <span class="rate-code">${info.flag} ${code}</span>
            <span class="rate-name">${info.name}</span>
          </div>
          <div class="rate-val">${info.symbol} ${converted.toLocaleString(undefined, {
        maximumFractionDigits: ['KRW', 'VND', 'JPY'].includes(code) ? 1 : 3
      })}</div>
          <div class="rate-krw">${perKrw}</div>
        </div>
      `;
    })
    .join('');

  popularRatesGrid.querySelectorAll('.rate-card').forEach((card) => {
    card.addEventListener('click', () => {
      toCurrencySel.value = card.dataset.code;
      convertCurrency('forward');
      showToast(`받는 통화가 ${card.dataset.code}(으)로 변경되었습니다.`);
    });
  });
}

async function fetchLiveExchangeRates() {
  rateStatusBadge.className = 'badge loading';
  rateStatusBadge.textContent = '실시간 환율 조회 중...';

  try {
    const res = await fetch('https://open.er-api.com/v6/latest/USD');
    if (!res.ok) throw new Error('API Error');
    const data = await res.json();
    if (data && data.rates) {
      exchangeRates = { ...exchangeRates, ...data.rates };
      rateStatusBadge.className = 'badge live';
      rateStatusBadge.textContent = '● 실시간 환율 연동됨';
      const updatedDate = data.time_last_update_utc
        ? new Date(data.time_last_update_utc).toLocaleString('ko-KR')
        : new Date().toLocaleString('ko-KR');
      rateUpdateTime.textContent = `최근 업데이트: ${updatedDate}`;
      convertCurrency('forward');
      return;
    }
  } catch (err) {
    // 2차 백업 API 시도
    try {
      const res2 = await fetch('https://api.frankfurter.dev/v1/latest?base=USD');
      if (res2.ok) {
        const data2 = await res2.json();
        if (data2 && data2.rates) {
          exchangeRates = { ...exchangeRates, ...data2.rates, USD: 1 };
          rateStatusBadge.className = 'badge live';
          rateStatusBadge.textContent = '● 실시간 환율 연동됨';
          rateUpdateTime.textContent = `기준일: ${data2.date}`;
          convertCurrency('forward');
          return;
        }
      }
    } catch (_) {
      // 오프라인 폴백 유지
    }
  }

  rateStatusBadge.className = 'badge offline';
  rateStatusBadge.textContent = '● 오프라인 기본 환율 모드';
  rateUpdateTime.textContent = '인터넷 연결 시 실시간 환율로 자동 갱신됩니다.';
  convertCurrency('forward');
}

fromCurrencySel.addEventListener('change', () => convertCurrency('forward'));
toCurrencySel.addEventListener('change', () => convertCurrency('forward'));
fromAmountInput.addEventListener('input', () => convertCurrency('forward'));
toAmountInput.addEventListener('input', () => convertCurrency('backward'));

document.getElementById('swapCurrencyBtn').addEventListener('click', () => {
  const temp = fromCurrencySel.value;
  fromCurrencySel.value = toCurrencySel.value;
  toCurrencySel.value = temp;
  convertCurrency('forward');
});

document.getElementById('refreshRatesBtn').addEventListener('click', () => {
  fetchLiveExchangeRates();
  showToast('최신 환율 정보를 다시 불러옵니다.');
});

document.querySelectorAll('.quick-amounts .chip-btn').forEach((btn) => {
  btn.addEventListener('click', () => {
    fromAmountInput.value = btn.dataset.amt;
    convertCurrency('forward');
  });
});

populateCurrencySelects();
fetchLiveExchangeRates();

// ==========================================
// 3. 다기능 단위 변환기
// ==========================================
const UNIT_CATEGORIES = {
  length: {
    name: '길이',
    defaultFrom: 'm',
    defaultTo: 'cm',
    units: {
      mm: { label: '밀리미터 (mm)', factor: 0.001 },
      cm: { label: '센티미터 (cm)', factor: 0.01 },
      m: { label: '미터 (m)', factor: 1 },
      km: { label: '킬로미터 (km)', factor: 1000 },
      in: { label: '인치 (inch)', factor: 0.0254 },
      ft: { label: '피트 (ft)', factor: 0.3048 },
      yd: { label: '야드 (yd)', factor: 0.9144 },
      mile: { label: '마일 (mile)', factor: 1609.344 },
      ja: { label: '자 / 척 (尺)', factor: 0.30303 },
      gan: { label: '간 (間)', factor: 1.81818 },
      ri: { label: '리 (里)', factor: 392.727 }
    }
  },
  weight: {
    name: '무게',
    defaultFrom: 'kg',
    defaultTo: 'lb',
    units: {
      mg: { label: '밀리그램 (mg)', factor: 0.000001 },
      g: { label: '그램 (g)', factor: 0.001 },
      kg: { label: '킬로그램 (kg)', factor: 1 },
      t: { label: '톤 (t)', factor: 1000 },
      oz: { label: '온스 (oz)', factor: 0.02834952 },
      lb: { label: '파운드 (lb)', factor: 0.45359237 },
      geun: { label: '근 (斤, 600g)', factor: 0.6 },
      don: { label: '돈 (錢, 3.75g)', factor: 0.00375 }
    }
  },
  area: {
    name: '넓이',
    defaultFrom: 'pyeong',
    defaultTo: 'm2',
    units: {
      cm2: { label: '제곱센티미터 (cm²)', factor: 0.0001 },
      m2: { label: '제곱미터 (m²)', factor: 1 },
      pyeong: { label: '평 (坪, 3.3058m²)', factor: 3.305785 },
      ha: { label: '헥타르 (ha)', factor: 10000 },
      km2: { label: '제곱킬로미터 (km²)', factor: 1000000 },
      ft2: { label: '제곱피트 (ft²)', factor: 0.092903 },
      acre: { label: '에이커 (acre)', factor: 4046.856 }
    }
  },
  volume: {
    name: '부피',
    defaultFrom: 'L',
    defaultTo: 'mL',
    units: {
      mL: { label: '밀리리터 (mL / cc)', factor: 0.001 },
      L: { label: '리터 (L)', factor: 1 },
      m3: { label: '세제곱미터 (m³)', factor: 1000 },
      tsp: { label: '티스푼 (tsp)', factor: 0.00492892 },
      tbsp: { label: '테이블스푼 (tbsp)', factor: 0.0147868 },
      floz: { label: '액량 온스 (fl oz)', factor: 0.0295735 },
      cup: { label: '컵 (US cup)', factor: 0.236588 },
      gal: { label: '갤런 (US gal)', factor: 3.78541 },
      doe: { label: '되 (升, 약 1.8L)', factor: 1.8039 },
      mal: { label: '말 (斗, 약 18L)', factor: 18.039 }
    }
  },
  temperature: {
    name: '온도',
    defaultFrom: 'C',
    defaultTo: 'F',
    custom: true,
    units: {
      C: { label: '섭씨 (°C)' },
      F: { label: '화씨 (°F)' },
      K: { label: '절대온도 (K)' }
    }
  },
  data: {
    name: '데이터 용량',
    defaultFrom: 'GB',
    defaultTo: 'MB',
    units: {
      B: { label: '바이트 (B)', factor: 1 },
      KB: { label: '킬로바이트 (KB)', factor: 1024 },
      MB: { label: '메가바이트 (MB)', factor: 1048576 },
      GB: { label: '기가바이트 (GB)', factor: 1073741824 },
      TB: { label: '테라바이트 (TB)', factor: 1099511627776 },
      PB: { label: '페타바이트 (PB)', factor: 1125899906842624 }
    }
  },
  speed: {
    name: '속도',
    defaultFrom: 'kmh',
    defaultTo: 'ms',
    units: {
      ms: { label: '초속 (m/s)', factor: 1 },
      kmh: { label: '시속 (km/h)', factor: 0.27777778 },
      mph: { label: '마일 시속 (mph)', factor: 0.44704 },
      knot: { label: '노트 (knot)', factor: 0.514444 },
      mach: { label: '마하 (Mach)', factor: 340.29 }
    }
  },
  time: {
    name: '시간',
    defaultFrom: 'hr',
    defaultTo: 'min',
    units: {
      sec: { label: '초 (sec)', factor: 1 },
      min: { label: '분 (min)', factor: 60 },
      hr: { label: '시간 (hr)', factor: 3600 },
      day: { label: '일 (day)', factor: 86400 },
      week: { label: '주 (week)', factor: 604800 },
      month: { label: '개월 (30일 기준)', factor: 2592000 },
      year: { label: '년 (365일 기준)', factor: 31536000 }
    }
  }
};

let currentCategory = 'length';
const fromUnitSel = document.getElementById('fromUnit');
const toUnitSel = document.getElementById('toUnit');
const fromUnitInput = document.getElementById('fromUnitValue');
const toUnitInput = document.getElementById('toUnitValue');
const unitFormulaBar = document.getElementById('unitFormulaBar');
const allUnitsGrid = document.getElementById('allUnitsGrid');

function convertTemperature(val, from, to) {
  if (from === to) return val;
  let celsius = val;
  if (from === 'F') celsius = ((val - 32) * 5) / 9;
  else if (from === 'K') celsius = val - 273.15;

  if (to === 'C') return celsius;
  if (to === 'F') return (celsius * 9) / 5 + 32;
  if (to === 'K') return celsius + 273.15;
  return val;
}

function convertSingleUnit(val, catKey, fromKey, toKey) {
  const cat = UNIT_CATEGORIES[catKey];
  if (cat.custom && catKey === 'temperature') {
    return convertTemperature(val, fromKey, toKey);
  }
  const fromFactor = cat.units[fromKey].factor;
  const toFactor = cat.units[toKey].factor;
  return (val * fromFactor) / toFactor;
}

function formatUnitVal(n) {
  if (!isFinite(n)) return '0';
  if (n === 0) return '0';
  const abs = Math.abs(n);
  if (abs >= 1e9 || (abs > 0 && abs < 1e-6)) {
    return n.toExponential(4);
  }
  return Number(n.toPrecision(8)).toString();
}

function loadUnitCategory(catKey) {
  currentCategory = catKey;
  const cat = UNIT_CATEGORIES[catKey];
  const html = Object.entries(cat.units)
    .map(([k, u]) => `<option value="${k}">${u.label}</option>`)
    .join('');
  fromUnitSel.innerHTML = html;
  toUnitSel.innerHTML = html;
  fromUnitSel.value = cat.defaultFrom;
  toUnitSel.value = cat.defaultTo;
  updateUnitConversion('forward');
}

function updateUnitConversion(direction = 'forward') {
  const fromKey = fromUnitSel.value;
  const toKey = toUnitSel.value;
  const cat = UNIT_CATEGORIES[currentCategory];

  if (direction === 'forward') {
    const v = parseFloat(fromUnitInput.value);
    if (isNaN(v)) {
      toUnitInput.value = '';
      return;
    }
    const res = convertSingleUnit(v, currentCategory, fromKey, toKey);
    toUnitInput.value = formatUnitVal(res);
  } else {
    const v = parseFloat(toUnitInput.value);
    if (isNaN(v)) {
      fromUnitInput.value = '';
      return;
    }
    const res = convertSingleUnit(v, currentCategory, toKey, fromKey);
    fromUnitInput.value = formatUnitVal(res);
  }

  const baseVal = parseFloat(fromUnitInput.value) || 0;
  const targetVal = parseFloat(toUnitInput.value) || 0;
  unitFormulaBar.textContent = `${baseVal.toLocaleString()} ${cat.units[fromKey].label} = ${Number(
    targetVal
  ).toLocaleString(undefined, { maximumFractionDigits: 6 })} ${cat.units[toKey].label}`;

  renderAllUnitsGrid(baseVal, fromKey);
}

function renderAllUnitsGrid(baseVal, fromKey) {
  const cat = UNIT_CATEGORIES[currentCategory];
  allUnitsGrid.innerHTML = Object.entries(cat.units)
    .map(([unitKey, unitObj]) => {
      const converted = convertSingleUnit(baseVal, currentCategory, fromKey, unitKey);
      const displayStr = Number(converted).toLocaleString(undefined, { maximumFractionDigits: 6 });
      return `
        <div class="unit-card" data-val="${formatUnitVal(converted)}" data-label="${unitObj.label}">
          <div class="rate-card-top">
            <span class="rate-name">${unitObj.label}</span>
          </div>
          <div class="rate-val">${displayStr}</div>
        </div>
      `;
    })
    .join('');

  allUnitsGrid.querySelectorAll('.unit-card').forEach((card) => {
    card.addEventListener('click', () => {
      navigator.clipboard.writeText(card.dataset.val);
      showToast(`${card.dataset.label}: ${card.dataset.val} 복사 완료!`);
    });
  });
}

document.querySelectorAll('#unitCategoryBar .cat-chip').forEach((btn) => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('#unitCategoryBar .cat-chip').forEach((b) => b.classList.remove('active'));
    btn.classList.add('active');
    loadUnitCategory(btn.dataset.cat);
  });
});

fromUnitSel.addEventListener('change', () => updateUnitConversion('forward'));
toUnitSel.addEventListener('change', () => updateUnitConversion('forward'));
fromUnitInput.addEventListener('input', () => updateUnitConversion('forward'));
toUnitInput.addEventListener('input', () => updateUnitConversion('backward'));

document.getElementById('swapUnitBtn').addEventListener('click', () => {
  const tmp = fromUnitSel.value;
  fromUnitSel.value = toUnitSel.value;
  toUnitSel.value = tmp;
  updateUnitConversion('forward');
});

loadUnitCategory('length');

// ==========================================
// 4. 댓글 작성 & 관리자 메뉴 (비밀번호: 1234)
// ==========================================
const ADMIN_PASSWORD = '1234';
let isAdminAuthenticated = false;
let pendingDeleteCommentId = null;

const defaultComments = [
  {
    id: 1,
    author: '관리자',
    content: '스마트 올인원 계산기에 오신 것을 환영합니다! 사용 후기나 건의사항을 자유롭게 남겨주세요.',
    date: '2026. 10. 26.'
  }
];

let commentsData = JSON.parse(localStorage.getItem('smart_calc_comments') || 'null');
if (!commentsData) {
  commentsData = defaultComments;
  localStorage.setItem('smart_calc_comments', JSON.stringify(commentsData));
}

const commentForm = document.getElementById('commentForm');
const commentAuthorInput = document.getElementById('commentAuthor');
const commentContentInput = document.getElementById('commentContent');
const commentListEl = document.getElementById('commentList');
const commentCountBadge = document.getElementById('commentCountBadge');

const adminToggleBtn = document.getElementById('adminToggleBtn');
const adminStatusBadge = document.getElementById('adminStatusBadge');
const adminPanel = document.getElementById('adminPanel');
const adminLoginBox = document.getElementById('adminLoginBox');
const adminControlBox = document.getElementById('adminControlBox');
const adminPasswordInput = document.getElementById('adminPasswordInput');
const adminLoginSubmit = document.getElementById('adminLoginSubmit');
const adminLoginCancel = document.getElementById('adminLoginCancel');
const adminLogoutBtn = document.getElementById('adminLogoutBtn');
const selectAllCommentsChk = document.getElementById('selectAllComments');
const deleteSelectedCommentsBtn = document.getElementById('deleteSelectedCommentsBtn');
const deleteAllCommentsBtn = document.getElementById('deleteAllCommentsBtn');

const deleteModal = document.getElementById('deleteModal');
const modalPasswordInput = document.getElementById('modalPasswordInput');
const modalConfirmDeleteBtn = document.getElementById('modalConfirmDeleteBtn');
const modalCancelBtn = document.getElementById('modalCancelBtn');

function escapeHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function saveComments() {
  localStorage.setItem('smart_calc_comments', JSON.stringify(commentsData));
}

function renderComments() {
  commentCountBadge.textContent = commentsData.length;

  if (!commentsData.length) {
    commentListEl.innerHTML = '<li class="empty-msg" style="margin: 20px 0;">등록된 댓글이 없습니다. 첫 번째 댓글을 남겨보세요!</li>';
    selectAllCommentsChk.checked = false;
    return;
  }

  commentListEl.innerHTML = commentsData
    .map(
      (item) => `
      <li class="comment-item" data-id="${item.id}">
        <div class="comment-left">
          ${
            isAdminAuthenticated
              ? `<input type="checkbox" class="comment-checkbox" data-id="${item.id}" />`
              : ''
          }
          <div class="comment-body">
            <div class="comment-meta">
              <span class="comment-author">${escapeHtml(item.author)}</span>
              <span class="comment-date">${escapeHtml(item.date)}</span>
            </div>
            <div class="comment-text">${escapeHtml(item.content)}</div>
          </div>
        </div>
        <button class="comment-del-btn" data-id="${item.id}" title="댓글 삭제 (관리자)">🗑️ 삭제</button>
      </li>
    `
    )
    .join('');

  commentListEl.querySelectorAll('.comment-del-btn').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = Number(btn.dataset.id);
      if (isAdminAuthenticated) {
        deleteCommentById(id);
      } else {
        openDeleteModal(id);
      }
    });
  });
}

function deleteCommentById(id) {
  commentsData = commentsData.filter((c) => c.id !== id);
  saveComments();
  renderComments();
  showToast('댓글이 삭제되었습니다.');
}

// 댓글 작성
commentForm.addEventListener('submit', (e) => {
  e.preventDefault();
  const author = commentAuthorInput.value.trim() || '익명';
  const content = commentContentInput.value.trim();
  if (!content) return;

  const now = new Date();
  const dateStr = now.toLocaleString('ko-KR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  });

  commentsData.unshift({
    id: Date.now(),
    author,
    content,
    date: dateStr
  });

  saveComments();
  commentContentInput.value = '';
  renderComments();
  showToast('댓글이 등록되었습니다!');
});

commentContentInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    commentForm.requestSubmit();
  }
});

// 관리자 메뉴 열기/닫기
adminToggleBtn.addEventListener('click', () => {
  const isVisible = adminPanel.style.display !== 'none';
  if (isVisible) {
    adminPanel.style.display = 'none';
  } else {
    adminPanel.style.display = 'block';
    if (isAdminAuthenticated) {
      adminLoginBox.style.display = 'none';
      adminControlBox.style.display = 'flex';
    } else {
      adminLoginBox.style.display = 'flex';
      adminControlBox.style.display = 'none';
      adminPasswordInput.value = '';
      adminPasswordInput.focus();
    }
  }
});

function verifyAdminLogin() {
  if (adminPasswordInput.value === ADMIN_PASSWORD) {
    isAdminAuthenticated = true;
    adminPasswordInput.value = '';
    adminLoginBox.style.display = 'none';
    adminControlBox.style.display = 'flex';
    adminStatusBadge.style.display = 'inline-block';
    adminToggleBtn.textContent = '🛡️ 관리자 메뉴 (열림)';
    renderComments();
    showToast('관리자 모드로 로그인되었습니다.');
  } else {
    showToast('비밀번호가 일치하지 않습니다.');
    adminPasswordInput.value = '';
    adminPasswordInput.focus();
  }
}

adminLoginSubmit.addEventListener('click', verifyAdminLogin);
adminPasswordInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') {
    e.preventDefault();
    verifyAdminLogin();
  }
});

adminLoginCancel.addEventListener('click', () => {
  adminPanel.style.display = 'none';
});

adminLogoutBtn.addEventListener('click', () => {
  isAdminAuthenticated = false;
  adminStatusBadge.style.display = 'none';
  adminPanel.style.display = 'none';
  adminToggleBtn.textContent = '🔒 관리자 메뉴';
  selectAllCommentsChk.checked = false;
  renderComments();
  showToast('관리자 모드가 해제되었습니다.');
});

// 전체 선택 체크박스
selectAllCommentsChk.addEventListener('change', () => {
  const checked = selectAllCommentsChk.checked;
  commentListEl.querySelectorAll('.comment-checkbox').forEach((chk) => {
    chk.checked = checked;
  });
});

// 선택 댓글 일괄 삭제
deleteSelectedCommentsBtn.addEventListener('click', () => {
  if (!isAdminAuthenticated) return;
  const checkedBoxes = Array.from(commentListEl.querySelectorAll('.comment-checkbox:checked'));
  if (!checkedBoxes.length) {
    showToast('삭제할 댓글을 먼저 선택해 주세요.');
    return;
  }
  const idsToRemove = new Set(checkedBoxes.map((chk) => Number(chk.dataset.id)));
  commentsData = commentsData.filter((c) => !idsToRemove.has(c.id));
  selectAllCommentsChk.checked = false;
  saveComments();
  renderComments();
  showToast(`${idsToRemove.size}개의 댓글이 삭제되었습니다.`);
});

// 전체 댓글 초기화
deleteAllCommentsBtn.addEventListener('click', () => {
  if (!isAdminAuthenticated) return;
  if (!commentsData.length) {
    showToast('삭제할 댓글이 없습니다.');
    return;
  }
  commentsData = [];
  selectAllCommentsChk.checked = false;
  saveComments();
  renderComments();
  showToast('모든 댓글이 삭제되었습니다.');
});

// 개별 댓글 삭제 모달
function openDeleteModal(commentId) {
  pendingDeleteCommentId = commentId;
  modalPasswordInput.value = '';
  deleteModal.style.display = 'flex';
  modalPasswordInput.focus();
}

function closeDeleteModal() {
  pendingDeleteCommentId = null;
  deleteModal.style.display = 'none';
}

function confirmModalDelete() {
  if (modalPasswordInput.value === ADMIN_PASSWORD) {
    if (pendingDeleteCommentId !== null) {
      deleteCommentById(pendingDeleteCommentId);
    }
    closeDeleteModal();
  } else {
    showToast('관리자 비밀번호가 올바르지 않습니다.');
    modalPasswordInput.value = '';
    modalPasswordInput.focus();
  }
}

modalConfirmDeleteBtn.addEventListener('click', confirmModalDelete);
modalPasswordInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') {
    e.preventDefault();
    confirmModalDelete();
  } else if (e.key === 'Escape') {
    closeDeleteModal();
  }
});
modalCancelBtn.addEventListener('click', closeDeleteModal);
deleteModal.addEventListener('click', (e) => {
  if (e.target === deleteModal) closeDeleteModal();
});

renderComments();
