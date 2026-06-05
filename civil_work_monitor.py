#!/usr/bin/env python3
"""
Alwadi Civil Work Tenders Monitor — Smart Contextual Analyzer
يحلل مناقصات الأعمال الإنشائية والمشاريع الحكومية، يفلتر المشاريع المستهدفة
🇱🇾 Libya-Only Filter Applied
"""

import json, smtplib, requests, hashlib, re, time, os
from datetime import datetime, timezone, timedelta
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from urllib.parse import urljoin, urlparse
from bs4 import BeautifulSoup

# Import Libya Monitor Filter
try:
    from libya_monitor_filter import libya_filter
    LIBYA_FILTER_ENABLED = True
except ImportError:
    LIBYA_FILTER_ENABLED = False

# Set timezone to GMT+2 (Libya)
GMT_PLUS_2 = timezone(timedelta(hours=2))

def now_gmt2():
    """Get current time in GMT+2"""
    return datetime.now(GMT_PLUS_2).replace(tzinfo=None)

# Try to import PDF reader
try:
    import PyPDF2
    HAS_PDF = True
except ImportError:
    HAS_PDF = False
    try:
        import pdfplumber
        HAS_PDF = True
    except ImportError:
        HAS_PDF = False

# ======================== CONFIG =========================
CONFIG = {
    "email":      "alwadidevices@gmail.com",
    "password":   "gbbh vvuu khso dzzd",
    "smtp_server": "smtp.gmail.com",
    "smtp_port":  587,
    "to_recipients": ["sales@alwadi.ly"],
    "cc_recipients": ["a.abuaysha@alwadi.ly", "a.khurwat@alwadi.ly"],
    "from_name":  "Alwadi Civil Work Tenders Monitor",
    "batch_size": 10,
    "libyantenders_email": "sales@alwadi.ly",
    "libyantenders_pass":  "Alwadi-Coms*2005",
}

SOURCES = {
    "libyantenders": {"url": "https://libyantenders.ly/",         "name": "Libya Tenders"},
    "noc":           {"url": "https://noc.ly/tenders/",           "name": "NOC"},
    "attaat":        {"url": "https://www.attaat.pm.gov.ly/",     "name": "Attaat"},
    "ungm":          {"url": "https://www.ungm.org/",             "name": "UNGM Procurement"},
}

STATE_FILE = "/home/solutions/.openclaw/civil_state.json"
LOG_FILE   = "/home/solutions/.openclaw/civil_log.txt"
CONTACTS_FILE = "/home/solutions/.openclaw/civil_contacts.csv"

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.5,ar;q=0.3",
    "Accept-Encoding": "gzip, deflate",
    "DNT": "1",
    "Connection": "keep-alive",
    "Upgrade-Insecure-Requests": "1",
    "Cache-Control": "max-age=0",
    "Referer": "https://www.google.com/"
}

REQUEST_DELAY = 2  # ثواني بين الطلبات

# ======================== CONTEXTUAL ANALYZER =========================

class CivilWorkAnalyzer:
    """
    محلل سياقي للأعمال الإنشائية — يقرأ النص الكامل للمناقصة ويحكم:
    1. هل هي مشروع أعمال إنشائية/تأهيل؟
    2. ما نوع المشروع (مبنى، بنية تحتية، إلخ)؟
    3. ما الحل المقترح؟
    4. ما القيمة المقدرة؟
    """

    # --- Civil work positive signals (context-aware) ---
    CIVIL_SIGNALS = {
        "أعمال إنشائية":       10, "construction works":      10, "civil works":          10,
        "تأهيل":              10, "rehabilitation":           10, "renovations":          9,
        "إعادة تأهيل":         10, "reconstruction":          9,  "building":             8,
        "بناء":               8,  "مشروع بناء":             10, "construction project":   10,
        "مبنى":               8,  "building project":          9,  "structure":            7,
        "إنشاء":              8,  "construction":              8,  "infrastructure":       9,
        "تحسين":              6,  "improvement":               6,  "upgrade":              6,
        "إصلاح":              7,  "repair":                    7,  "maintenance":          6,
        "تجديد":              8,  "renovation":                9,  "refurbishment":        8,
        "رفع":                5,  "upgrading":                 6,  "expanding":            6,
        "مركز تدريب":         8,  "training centre":           8,  "training center":      8,
        "مركز تطوير النساء":   9,  "women development":         8,  "centre":               5,
        "مدرسة":              7,  "school":                    7,  "educational facility":  8,
        "مستشفى":             7,  "hospital":                  7,  "clinic":               6,
        "مركز صحي":           7,  "health facility":           8,  "medical facility":     8,
        "مكتب حكومي":         7,  "government building":       8,  "government office":    7,
        "مبنى بلدية":         8,  "municipality building":     8,  "municipal facility":   8,
        "مرافق عامة":         7,  "public facility":           8,  "public building":      8,
        "أساس":              6,  "foundation":                6,  "structural":           7,
        "أسقف":              6,  "roof":                      6,  "roofing":              7,
        "أرضية":             6,  "floor":                     6,  "flooring":             7,
        "جدران":             6,  "walls":                     6,  "partitions":           6,
        "أبواب":             5,  "doors":                     5,  "windows":              5,
        "نوافذ":             5,  "electrical installation":   7,  "plumbing":             7,
        "تمديدات كهربائية":    7,  "تمديدات صحية":            7,  "water supply":         6,
        "نظام صرف":          6,  "drainage":                  6,  "sanitation":           6,
        "معالجة المياه":      7,  "waste management":          6,  "garbage collection":   5,
        "كميات الكمية":       8,  "bill of quantities":        9,  "boq":                  9,
        "رسومات هندسية":      8,  "technical drawings":        9,  "architectural drawings": 9,
        "تصاميم":             7,  "specifications":            8,  "contract documents":   8,
        "مواصفات":            8,  "measurement basis":         8,  "construction contract": 9,
        "giz":               8,  "deutsche gesellschaft":      8,  "german cooperation":   7,
        "municipality":       7,  "بلدية":                    7,  "ministry":             6,
        "وزارة":              6,  "مشروع حكومي":             8,  "government project":    8,
        "مقاول":              6,  "contractor":                6,  "construction company":  7,
        "عطاء":               6,  "invitation to tender":      8,  "tender":               7,
        "مناقصة":             8,  "bid":                       6,  "proposal":             6,
        "عقد":                5,  "contract":                  5,  "agreement":            5,
        "ملف مرفق":           3,  # File attachment indicator
    }

    # --- Strong non-civil signals that kill classification ---
    NOT_CIVIL_SIGNALS = {
        "solar": -10, "photovoltaic": -10, "pv system": -10, "solar panel": -10,
        "شمسي": -10, "خلايا شمسية": -10, "طاقة شمسية": -10,
        "it system": -8, "network": -8, "software": -8, "نظام معلومات": -8,
        "office equipment": -8, "مستلزمات مكاتب": -8, "stationery": -10,
        "furniture": -9, "أثاث": -9, "vehicles": -9, "مركبات": -9,
        "food": -8, "مواد غذائية": -8, "medical equipment": -8, "معدات طبية": -8,
        "security guard": -10, "حارسة أمنية": -10,
    }

    # --- Labour-only signals (pure staffing with no construction) ---
    LABOUR_ONLY_SIGNALS = {
        "عمالة": 5, "labour": 5, "staffing": 5, "staff": 5,
        "توظيف": 5, "recruitment": 5, "employees": 5, "موظفين": 5,
        "تقنية مساعدة": 5, "technical support": 5, "support staff": 5,
        "فنية": 3, "technical": 3,
    }

    # --- Target sectors ---
    SECTORS = {
        "مباني حكومية": {
            "keywords": ["حكومي", "وزارة", "ministry", "government", "مكتب", "office",
                        "مبنى إداري", "administrative building"],
            "color": "#FF8C00", "icon": "🏛️"
        },
        "مباني تعليمية": {
            "keywords": ["مدرسة", "school", "جامعة", "university", "كلية", "college",
                        "معهد", "institute", "تدريب", "training"],
            "color": "#C55A11", "icon": "🎓"
        },
        "مرافق صحية": {
            "keywords": ["مستشفى", "hospital", "عيادة", "clinic", "مركز صحي",
                        "health facility", "medical center"],
            "color": "#C00000", "icon": "🏥"
        },
        "بنية تحتية": {
            "keywords": ["طريق", "road", "جسر", "bridge", "مياه", "water",
                        "كهرباء", "electricity", "صرف", "drainage"],
            "color": "#00B050", "icon": "🏗️"
        },
        "مرافق بلدية": {
            "keywords": ["بلدية", "municipality", "محلي", "local", "عام", "public"],
            "color": "#4472C4", "icon": "🏘️"
        },
    }

    # --- Solution mapping (context-aware) ---
    SOLUTIONS = [
        {
            "triggers":   ["rehabilitation", "تأهيل", "إعادة تأهيل", "renovations",
                          "تجديد", "refurbishment"],
            "solution":   "مشروع تأهيل ومرافق — Rehabilitation & Facilities Project",
            "value_range": (100_000, 2_000_000),
        },
        {
            "triggers":   ["training centre", "مركز تدريب", "women development",
                          "تطوير النساء", "development center"],
            "solution":   "مركز تطوير وتدريب — Training & Development Centre",
            "value_range": (150_000, 1_500_000),
        },
        {
            "triggers":   ["school", "مدرسة", "educational", "تعليمي", "university",
                          "جامعة"],
            "solution":   "مشروع بناء تعليمي — Educational Building Project",
            "value_range": (200_000, 3_000_000),
        },
        {
            "triggers":   ["hospital", "مستشفى", "clinic", "عيادة", "health facility",
                          "مركز صحي"],
            "solution":   "مرفق صحي — Healthcare Facility Construction",
            "value_range": (300_000, 5_000_000),
        },
        {
            "triggers":   ["road", "طريق", "bridge", "جسر", "infrastructure",
                          "بنية تحتية"],
            "solution":   "مشروع بنية تحتية — Infrastructure Project",
            "value_range": (500_000, 10_000_000),
        },
        {
            "triggers":   ["water", "مياه", "drainage", "صرف", "sanitation",
                          "water supply", "إمدادات مياه"],
            "solution":   "نظام المياه والصرف — Water & Sewage System",
            "value_range": (200_000, 2_000_000),
        },
        {
            "triggers":   ["construction works", "أعمال إنشائية", "civil works",
                          "building", "مبنى", "construction"],
            "solution":   "مشروع أعمال إنشائية — Construction Works Project",
            "value_range": (100_000, 3_000_000),
        },
    ]

    def analyze(self, title: str, full_text: str) -> dict | None:
        """
        يحلل المناقصة ويرجع dict أو None إذا رُفضت
        🇱🇾 يفلتر المشاريع الليبية فقط
        """
        combined = (title + " " + full_text).lower()
        combined_orig = title + " " + full_text

        # --- 0. فلتر ليبيا ---
        if LIBYA_FILTER_ENABLED:
            if not libya_filter.is_libya_related(title + " " + full_text):
                return None  # رفض المشاريع غير الليبية

        # --- 0b. فلتر عمالة فقط (بدون عمل إنشائي فعلي) ---
        if self._is_labour_only(combined):
            return None  # رفض المناقصات التي هي عمالة فقط

        # --- 1. حساب نقاط الأعمال الإنشائية ---
        civil_score = 0
        for kw, score in self.CIVIL_SIGNALS.items():
            if kw.lower() in combined:
                civil_score += score

        # --- 2. خصم نقاط non-civil ---
        not_civil_score = 0
        for kw, penalty in self.NOT_CIVIL_SIGNALS.items():
            if kw.lower() in combined:
                not_civil_score += penalty

        net_score = civil_score + not_civil_score

        # رفض إذا النتيجة غير كافية — مرن أكثر لاكتشاف مشاريع البناء
        threshold = 3 if full_text.strip() else 2
        if net_score < threshold:
            return None

        # --- 3. تحديد القطاع ---
        sector_name = "عام"
        sector_meta = {"color": "#555", "icon": "🏗️"}
        best_sector_score = 0

        for sec_name, sec_data in self.SECTORS.items():
            score = sum(1 for k in sec_data["keywords"] if k.lower() in combined)
            if score > best_sector_score:
                best_sector_score = score
                sector_name = sec_name
                sector_meta = sec_data

        # --- 4. اقتراح الحل ---
        matched_solutions = []

        for rule in self.SOLUTIONS:
            if any(t.lower() in combined for t in rule["triggers"]):
                matched_solutions.append(rule["solution"])

        if not matched_solutions:
            matched_solutions = ["مشروع أعمال إنشائية — يحتاج دراسة تفصيلية"]

        # --- 5. استخراج أو تقدير القيمة (بخفض 35%) ---
        extracted_value = self._extract_value(combined_orig)
        if extracted_value:
            value_str = extracted_value
        else:
            # تقدير بناءً على الوصف
            estimated, reason = self._estimate_value_from_description(combined_orig)
            value_str = f"${self._fmt(estimated)} (تقدير: {reason})"

        return {
            "sector":    sector_name,
            "sector_color": sector_meta["color"],
            "sector_icon":  sector_meta["icon"],
            "solutions": matched_solutions[:2],
            "value":     value_str,
            "civil_score":  net_score,
        }

    def _extract_value(self, text: str) -> str:
        """استخراج القيمة المذكورة وتطبيق خصم 70% (الضرب في 0.30)"""
        patterns = [
            r'(\d+(?:[,\.]\d+)*)\s*(مليون|million)\s*(دينار|دولار|ليبي|USD|LYD|LD)?',
            r'(\d+(?:[,\.]\d+)*)\s*(ألف|thousand)\s*(دينار|دولار|ليبي|USD|LYD|LD)?',
            r'(USD|LYD|LD|\$)\s*(\d+(?:[,\.]\d+)*)',
            r'(\d+(?:[,\.]\d+)*)\s*(LYD|LD|دينار ليبي)',
            r'قيمة[ها]?\s*[:\s]+(\d+(?:[,\.]\d+)*)',
            r'budget[:\s]+(\d+(?:[,\.]\d+)*)',
        ]
        for p in patterns:
            m = re.search(p, text, re.IGNORECASE)
            if m:
                value_text = m.group(0).strip()[:60]
                value_num = self._parse_value_number(text, m)
                if value_num:
                    final = int(value_num * 0.30)
                    return f"${self._fmt(final)} (المذكورة: {value_text} × 0.30)"
                return value_text
        return ""

    def _parse_value_number(self, text: str, match) -> int | None:
        """استخراج الرقم من القيمة المذكورة"""
        try:
            full_match = match.group(0)
            # استخراج الأرقام
            numbers = re.findall(r'\d+(?:[,\.]\d+)*', full_match)
            if not numbers:
                return None

            num_str = numbers[0].replace(',', '')
            if '.' in num_str:
                num = int(float(num_str))
            else:
                num = int(num_str)

            # التحقق من المضاعف (ألف، مليون)
            if 'مليون' in full_match.lower() or 'million' in full_match.lower():
                num *= 1_000_000
            elif 'ألف' in full_match.lower() or 'thousand' in full_match.lower():
                num *= 1_000

            return num
        except:
            return None

    def _estimate_value_from_description(self, text: str) -> tuple[int, str]:
        """
        تقدير القيمة بناءً على الشغل والأجهزة ثم خفض 35%
        """
        # استخراج المساحة
        area_match = re.search(r'(\d+)\s*(?:متر مربع|m²|sqm|square meter)', text, re.IGNORECASE)
        area = int(area_match.group(1)) if area_match else 0

        # عد المكونات والأجهزة
        components = ['أساس', 'جدران', 'أسقف', 'أرضية', 'تمديدات كهربائية', 'تمديدات صحية',
                     'أبواب', 'نوافذ', 'إضاءة', 'رصف', 'درج', 'معدات', 'تجهيزات']
        component_count = sum(1 for c in components if c.lower() in text.lower())

        # نوع المشروع
        is_rehab = any(w in text.lower() for w in ['تأهيل', 'rehabilitation', 'تجديد'])
        is_new = any(w in text.lower() for w in ['إنشاء', 'بناء جديد', 'new construction'])
        is_rebuild = any(w in text.lower() for w in ['إعادة بناء', 'reconstruction'])

        # حساب التكلفة الفعلية (شغل + أجهزة)
        total_cost = 0

        if area > 0:
            # تكلفة بناءً على المساحة والنوع
            if is_rehab:
                cost_per_sqm = 150  # شغل + مواد تأهيل
            elif is_rebuild:
                cost_per_sqm = 220
            else:
                cost_per_sqm = 280  # بناء جديد (شغل + مواد + أجهزة)
            total_cost = area * cost_per_sqm
        else:
            # تكلفة بناءً على المكونات والأجهزة
            if component_count >= 5:
                total_cost = 900_000  # مشروع شامل (شغل + أجهزة)
            elif component_count >= 3:
                total_cost = 600_000
            else:
                total_cost = 300_000  # مشروع صغير

        # تطبيق خفض 35% على التكلفة الإجمالية
        final_value = int(total_cost * 0.65)
        reason = f"مساحة: {area}m² - خفض 35%" if area > 0 else f"مكونات: {component_count} - خفض 35%"

        return final_value, reason

    def _fmt(self, n: int) -> str:
        if n >= 1_000_000:
            return f"{n/1_000_000:.1f}M"
        if n >= 1_000:
            return f"{n/1_000:.0f}K"
        return str(n)

    def _is_labour_only(self, combined: str) -> bool:
        """
        Check if tender is pure labour/staffing with no actual construction component.
        Returns True if should be rejected (labour-only).
        """
        labour_score = 0
        for kw in self.LABOUR_ONLY_SIGNALS.keys():
            if kw.lower() in combined:
                labour_score += self.LABOUR_ONLY_SIGNALS[kw]

        # If labour signals found but NO strong civil work signals, it's labour-only
        if labour_score > 0:
            # Count strong civil work keywords (score >= 7)
            strong_civil_count = sum(1 for kw, score in self.CIVIL_SIGNALS.items()
                                    if score >= 7 and kw.lower() in combined)
            # If labour signals present but few/no strong civil signals, reject
            if strong_civil_count <= 1:
                return True
        return False


# ======================== HELPERS =========================

def log(msg):
    ts = now_gmt2().strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{ts} GMT+2] {msg}"
    print(line)
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(line + "\n")

def load_state():
    if os.path.exists(STATE_FILE):
        with open(STATE_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    return {
        "seen_ids": [],
        "last_check": None,
        "civil_tenders": []
    }

def save_state(state):
    with open(STATE_FILE, "w", encoding="utf-8") as f:
        json.dump(state, f, ensure_ascii=False, indent=2)

def make_id(text):
    return hashlib.md5(text.strip().encode()).hexdigest()[:12]

def clean_text(raw):
    text = re.sub(r'\s+', ' ', raw).strip()
    text = re.sub(r'([a-zA-Z])([ء-ي])', r'\1 \2', text)
    text = re.sub(r'([ء-ي])([a-zA-Z])', r'\1 \2', text)
    return text

def fetch_page(url, timeout=12, retry_count=0):
    import random
    try:
        time.sleep(REQUEST_DELAY + random.uniform(0, 1))
        r = requests.get(url, timeout=timeout, headers=HEADERS)
        if r.status_code == 403:
            if retry_count < 2:
                log(f"  ⚠️  403 Forbidden على {url} — إعادة محاولة...")
                time.sleep(5 + random.uniform(0, 3))
                return fetch_page(url, timeout=timeout, retry_count=retry_count + 1)
            else:
                log(f"  ⚠️  فشل جلب {url}: 403 Forbidden (بعد إعادة محاولات)")
                return None
        r.raise_for_status()
        return r.text
    except requests.exceptions.Timeout:
        log(f"  ⚠️  انتهاء الوقت عند جلب {url}")
        return None
    except requests.exceptions.RequestException as e:
        log(f"  ⚠️  فشل جلب {url}: {e}")
        return None
    except Exception as e:
        log(f"  ⚠️  خطأ غير متوقع عند جلب {url}: {e}")
        return None

def is_individual_url(href, base_url):
    path = urlparse(href).path
    base_path = urlparse(base_url).path.rstrip('/')
    return (path.rstrip('/') != base_path and
            bool(re.search(r'/\d+|/tender|/detail|/view|/show|/item|/post|/bid|page=\d', href, re.I)))

def get_page_text(url):
    html = fetch_page(url, timeout=10)
    if not html:
        return ""
    soup = BeautifulSoup(html, "html.parser")
    for tag in soup(["nav", "header", "footer", "script", "style", "aside"]):
        tag.decompose()
    return clean_text(soup.get_text(separator=" ", strip=True)[:3000])

def extract_contacts(text, organization="", tender_title="", source=""):
    """Extract email and phone contacts from text"""
    contacts = []

    email_pattern = r'\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b'
    emails = re.findall(email_pattern, text)

    phone_pattern = r'(?:\+218|00218|0)?(?:9[0-2]|2[1-3])\d{7,8}'
    phones = re.findall(phone_pattern, text)

    for email in set(emails):
        if email.lower() in ['test@test.com', 'example@example.com']:
            continue
        contact = {
            'date_found': now_gmt2().strftime("%Y-%m-%d"),
            'organization': organization,
            'contact_name': '',
            'job_title': '',
            'email': email,
            'phone': '',
            'tender_title': tender_title,
            'sector': '',
            'source': source
        }
        contacts.append(contact)

    for phone in set(phones):
        if not phone.startswith('+'):
            phone = '+218' + phone.lstrip('0') if phone.startswith('0') else '+218' + phone
        if len(phone) < 10:
            continue
        contact = {
            'date_found': now_gmt2().strftime("%Y-%m-%d"),
            'organization': organization,
            'contact_name': '',
            'job_title': '',
            'email': '',
            'phone': phone,
            'tender_title': tender_title,
            'sector': '',
            'source': source
        }
        contacts.append(contact)

    return contacts

def save_contacts_to_csv(contacts):
    """Save contacts to CSV file, avoiding duplicates"""
    if not contacts:
        return

    import csv

    existing_emails = set()
    existing_phones = set()

    if os.path.exists(CONTACTS_FILE):
        try:
            with open(CONTACTS_FILE, 'r', encoding='utf-8') as f:
                reader = csv.DictReader(f)
                for row in reader:
                    if row.get('email'):
                        existing_emails.add(row['email'].lower())
                    if row.get('phone'):
                        existing_phones.add(row['phone'])
        except:
            pass

    new_contacts = []
    for c in contacts:
        email_key = c['email'].lower() if c['email'] else None
        phone_key = c['phone'] if c['phone'] else None

        if email_key and email_key not in existing_emails:
            new_contacts.append(c)
            existing_emails.add(email_key)
        elif phone_key and phone_key not in existing_phones:
            new_contacts.append(c)
            existing_phones.add(phone_key)

    if not new_contacts:
        return

    fieldnames = ['date_found', 'organization', 'contact_name', 'job_title', 'email', 'phone', 'tender_title', 'sector', 'source']

    try:
        file_exists = os.path.exists(CONTACTS_FILE)
        with open(CONTACTS_FILE, 'a', encoding='utf-8', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            if not file_exists:
                writer.writeheader()
            for contact in new_contacts:
                writer.writerow(contact)
    except Exception as e:
        log(f"  ⚠️  خطأ حفظ جهات الاتصال: {e}")

# ======================== SCRAPER =========================

def scrape_libyantenders():
    """يجلب مناقصات الأعمال الإنشائية عبر API المصادق عليه"""
    s = requests.Session()
    s.headers.update({"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/120"})

    try:
        r = s.get("https://libyantenders.ly/tds/login", timeout=15)
        soup = BeautifulSoup(r.text, "html.parser")
        csrf = soup.find("meta", {"name": "csrf-token"})

        if csrf:
            csrf_token = csrf.get("content", "")
            s.post("https://libyantenders.ly/tds/login",
                   data={"_token": csrf_token,
                         "email":    CONFIG["libyantenders_email"],
                         "password": CONFIG["libyantenders_pass"]},
                   timeout=15, allow_redirects=True)
    except Exception as e:
        log(f"  ⚠️  Libya Tenders login failed: {e}")
        return []

    xsrf = requests.utils.unquote(s.cookies.get("XSRF-TOKEN", ""))
    h = {"Accept": "application/json",
         "X-Requested-With": "XMLHttpRequest",
         "X-XSRF-TOKEN": xsrf,
         "Referer": "https://libyantenders.ly/tds/resources/tenders"}

    try:
        r2 = s.get("https://libyantenders.ly/nova-api/tenders?perPage=100&orderBy=id&orderByDirection=desc",
                   timeout=15, headers=h)
        data = r2.json()
    except Exception as e:
        log(f"  ⚠️  Libya Tenders API error: {e}")
        return []

    results = []
    for res in data.get("resources", []):
        tid = res["id"]["value"]
        fields = {}
        for f in res.get("fields", []):
            attr = f.get("attribute", "")
            val  = f.get("displayedAs") or f.get("value", "") or ""
            if attr and attr not in fields:
                fields[attr] = str(val).strip()

        title  = clean_text(fields.get("title", ""))
        sector = fields.get("sector", "")
        url    = f"https://libyantenders.ly/tds/resources/tenders/{tid}"

        if title:
            results.append((title, url, sector))

    return results


def extract_tender_links(soup, src_url):
    """استخراج روابط المناقصات مع عناوينها"""
    VIEW_PHRASES = {"عرض التفاصيل", "عرض", "view", "details", "تفاصيل", "read more", "اقرأ المزيد"}
    pairs   = []
    seen    = set()

    for a in soup.find_all("a", href=True):
        href = a["href"].strip()
        if not href or href.startswith("#") or href.startswith("javascript"):
            continue
        if not href.startswith("http"):
            href = urljoin(src_url, href)

        link_text = clean_text(a.get_text(separator=" ", strip=True))

        if not is_individual_url(href, src_url):
            if len(link_text) > 20 and link_text.lower() not in VIEW_PHRASES:
                key = make_id(link_text)
                if key not in seen:
                    seen.add(key)
                    pairs.append((link_text[:250], href, False))
            continue

        if href in seen:
            continue
        seen.add(href)

        if link_text.lower() in VIEW_PHRASES or len(link_text) < 10:
            parent = a.find_parent(["div", "article", "li", "tr", "section", "p"])
            if parent:
                parent_text = clean_text(parent.get_text(separator=" ", strip=True))
                parent_text = re.sub(r'(عرض التفاصيل|عرض|view|details|تفاصيل)', ' ', parent_text, flags=re.IGNORECASE)
                parent_text = re.sub(r'\s+', ' ', parent_text).strip()
                if len(parent_text) > 15:
                    pairs.append((parent_text[:250], href, True))
        else:
            pairs.append((link_text[:250], href, True))

    return pairs


def scrape_source(src_url, src_name):
    log(f"📡 فحص: {src_name}")
    html = fetch_page(src_url)
    if not html:
        return []

    soup = BeautifulSoup(html, "html.parser")
    analyzer = CivilWorkAnalyzer()
    results = []
    seen_ids = set()

    pairs = extract_tender_links(soup, src_url)
    log(f"   → وجدت {len(pairs)} رابط مناقصة للتحليل")

    for title, href, has_detail in pairs:
        tid = make_id(make_id(title) + make_id(href))
        if tid in seen_ids:
            continue
        seen_ids.add(tid)

        detail_text = ""
        if has_detail:
            detail_text = get_page_text(href)
            time.sleep(0.5)

        result = analyzer.analyze(title, detail_text)
        if result is None:
            continue

        tender_dict = {
            "id":           tid,
            "title":        title[:220],
            "link":         href,
            "source":       src_name,
            "sector":       result["sector"],
            "sector_color": result["sector_color"],
            "sector_icon":  result["sector_icon"],
            "solutions":    result["solutions"],
            "value":        result["value"],
            "civil_score":  result["civil_score"],
            "found_at":     now_gmt2().strftime("%Y-%m-%d %H:%M GMT+2"),
        }
        results.append(tender_dict)

        contacts = extract_contacts(
            detail_text or title,
            organization="",
            tender_title=title[:100],
            source=src_name
        )
        if contacts:
            save_contacts_to_csv(contacts)

    results.sort(key=lambda x: x["civil_score"], reverse=True)
    log(f"   → {len(results)} مناقصة أعمال إنشائية مؤهلة")
    return results[:20]

# ======================== EMAIL =========================

def build_email(tenders, part_num=None, total_parts=None):
    part_label = f" — الجزء {part_num}/{total_parts}" if total_parts and total_parts > 1 else ""
    rows = ""
    for i, t in enumerate(tenders, 1):
        bg = "#f7f9fc" if i % 2 == 0 else "white"
        sol_html = "<br>".join(
            f"<span style='color:#FF8C00'>• {s}</span>" for s in t["solutions"]
        )
        rows += f"""
        <tr style="background:{bg};vertical-align:top">
          <td style="padding:11px 8px;border:1px solid #dde;text-align:center;
                     color:#999;font-size:12px;width:26px">{i}</td>
          <td style="padding:11px 10px;border:1px solid #dde;line-height:1.7">
            <div style="font-size:14px;font-weight:bold">{t['title']}</div>
            <div style="margin-top:5px">
              <span style="background:{t['sector_color']};color:white;padding:2px 8px;
                           border-radius:3px;font-size:11px">{t['sector_icon']} {t['sector']}</span>
              &nbsp;<span style="color:#999;font-size:11px">{t['source']} | {t['found_at']}</span>
            </div>
          </td>
          <td style="padding:11px 10px;border:1px solid #dde;font-size:12px;
                     line-height:1.8;min-width:200px">{sol_html}</td>
          <td style="padding:11px 10px;border:1px solid #dde;font-size:12px;
                     min-width:130px;color:#FF8C00">
            <strong>{t['value']}</strong>
          </td>
          <td style="padding:11px 8px;border:1px solid #dde;text-align:center;width:55px">
            <a href="{t['link']}" style="background:#FF8C00;color:white;padding:5px 10px;
               border-radius:4px;text-decoration:none;font-size:11px">عرض</a>
          </td>
        </tr>"""

    cards_html = ""
    for i, t in enumerate(tenders, 1):
        sol_html = "<br>".join(f"• {s}" for s in t["solutions"])
        cards_html += f"""        <div class="tender-card">
          <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px">
            <div style="flex:1">
              <div class="card-title">{t['title']}</div>
              <div class="card-badge" style="background:{t['sector_color']}">{t['sector_icon']} {t['sector']}</div>
              <div class="card-meta">{t['source']} | {t['found_at']}</div>
            </div>
            <div style="font-size:18px;color:#999">#{i}</div>
          </div>
          <div class="card-solution">{sol_html}</div>
          <div class="card-value">القيمة: {t['value']}</div>
          <a href="{t['link']}" class="card-button">عرض التفاصيل</a>
        </div>
"""

    return f"""
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <style>
    @media (max-width: 640px) {{
      body {{padding: 8px !important;}}
      .tender-table {{display: none;}}
      .tender-cards {{display: block;}}
      .tender-card {{background: white;border: 1px solid #dde;border-radius: 8px;padding: 12px;
                    margin-bottom: 12px;box-shadow: 0 1px 3px rgba(0,0,0,0.1);}}
      .card-title {{font-size: 13px;font-weight: bold;margin-bottom: 8px;line-height: 1.4;}}
      .card-badge {{display: inline-block;padding: 3px 8px;border-radius: 3px;color: white;
                   font-size: 10px;margin-bottom: 8px;}}
      .card-meta {{color: #999;font-size: 10px;margin-bottom: 8px;}}
      .card-solution {{color: #FF8C00;font-size: 11px;line-height: 1.6;margin-bottom: 8px;}}
      .card-value {{color: #FF8C00;font-weight: bold;margin-bottom: 8px;font-size: 12px;}}
      .card-button {{display: inline-block;background: #FF8C00;color: white;padding: 6px 12px;
                    border-radius: 4px;text-decoration: none;font-size: 11px;width: 100%;
                    text-align: center;box-sizing: border-box;}}
    }}
    @media (min-width: 641px) {{
      .tender-cards {{display: none;}}
    }}
  </style>
</head>
<body style="font-family:Tahoma,Arial,sans-serif;direction:rtl;background:#f5f0e8;padding:16px;margin:0">
  <div style="max-width:900px;margin:auto;background:white;border-radius:10px;
              overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.13)">
    <div style="background:#FF8C00;color:white;padding:18px 22px">
      <h2 style="margin:0;font-size:17px">
        🏗️ مناقصات الأعمال الإنشائية — Alwadi Civil Work Monitor | Enhanced Version{part_label}
      </h2>
      <p style="margin:6px 0 0;font-size:12px;opacity:0.8">
        {now_gmt2().strftime('%Y-%m-%d %H:%M GMT+2')} &nbsp;|&nbsp;
        عدد المناقصات: {len(tenders)} &nbsp;|&nbsp;
        المصادر: Libya Tenders · NOC · Attaat · UNDP
      </p>
    </div>
    <div style="padding:18px 20px">
      <p style="margin-top:0;font-size:14px">
        السلام عليكم Team،<br>
        فيما يلي مناقصات الأعمال الإنشائية والتأهيل الجديدة من جميع مناطق ليبيا - جميع الفئات المالية.
        عمود <strong>نوع المشروع</strong> يعطيكم فكرة أولية، وعمود
        <strong>القيمة</strong> إما مذكور في المناقصة أو تقدير بناءً على نوع المشروع.
      </p>

      <!-- Desktop Table View -->
      <table class="tender-table" style="width:100%;border-collapse:collapse">
        <thead>
          <tr style="background:#FF8C00;color:white;font-size:12px">
            <th style="padding:10px 8px;border:1px solid #aab">#</th>
            <th style="padding:10px;border:1px solid #aab;text-align:right">المناقصة والقطاع</th>
            <th style="padding:10px;border:1px solid #aab;text-align:right">نوع المشروع</th>
            <th style="padding:10px;border:1px solid #aab;text-align:right">القيمة (USD)</th>
            <th style="padding:10px;border:1px solid #aab">رابط</th>
          </tr>
        </thead>
        <tbody>{rows}</tbody>
      </table>

      <!-- Mobile Card View -->
      <div class="tender-cards">
{cards_html}      </div>

      <p style="color:#bbb;font-size:10px;margin-top:14px;
                border-top:1px solid #eee;padding-top:10px">
        Alwadi Communications · تحليل تلقائي لمناقصات الأعمال الإنشائية · يُرجى التحقق من الموقع للتفاصيل الكاملة
      </p>
    </div>
  </div>
</body>
</html>"""

# ======================== SEND =========================

def send_email(subject, html_body):
    plain = f"Alwadi Civil Work Monitor\n{subject}\n\nيرجى قراءة هذا الإيميل بتنسيق HTML.\n\nAlwadi Communications"
    try:
        msg = MIMEMultipart("alternative")
        msg["Subject"]  = subject
        msg["From"]     = f"{CONFIG['from_name']} <{CONFIG['email']}>"
        msg["To"]       = ", ".join(CONFIG["to_recipients"])
        msg["Cc"]       = ", ".join(CONFIG["cc_recipients"])
        msg["Reply-To"] = CONFIG["email"]
        msg.attach(MIMEText(plain, "plain", "utf-8"))
        msg.attach(MIMEText(html_body, "html", "utf-8"))

        with smtplib.SMTP(CONFIG["smtp_server"], CONFIG["smtp_port"]) as s:
            s.starttls()
            s.login(CONFIG["email"], CONFIG["password"])
            all_recipients = CONFIG["to_recipients"] + CONFIG["cc_recipients"]
            s.send_message(msg, from_addr=CONFIG["email"], to_addrs=all_recipients)

        log(f"✅ إيميل أُرسل: {subject[:70]}")
        return True
    except Exception as e:
        log(f"❌ خطأ إرسال: {e}")
        return False

def send_no_tenders_email():
    """إرسال إشعار عدم وجود مناقصات جديدة"""
    subject = f"No New Civil Works — {now_gmt2().strftime('%Y-%m-%d')}"
    html_body = f"""
<html>
<head>
  <meta charset="utf-8">
</head>
<body style="font-family:Tahoma,Arial,sans-serif;direction:rtl;background:#f5f0e8;padding:16px;margin:0">
  <div style="max-width:600px;margin:auto;background:white;border-radius:10px;
              overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.13)">
    <div style="background:#FF8C00;color:white;padding:18px 22px">
      <h2 style="margin:0;font-size:17px">
        🏗️ بدون مناقصات أعمال إنشائية جديدة
      </h2>
      <p style="margin:6px 0 0;font-size:12px;opacity:0.8">
        {now_gmt2().strftime('%Y-%m-%d %H:%M GMT+2')}
      </p>
    </div>
    <div style="padding:18px 20px">
      <p style="margin-top:0;font-size:14px;color:#666">
        السلام عليكم Team،<br><br>
        لم يتم العثور على مناقصات أعمال إنشائية جديدة خلال آخر فحص.
      </p>
      <p style="color:#999;font-size:12px;border-top:1px solid #eee;padding-top:10px">
        Alwadi Communications · مراقبة مناقصات الأعمال الإنشائية التلقائية
      </p>
    </div>
  </div>
</body>
</html>"""
    return send_email(subject, html_body)

# ======================== MAIN =========================

def main():
    log("=" * 55)
    log("🔍 بدء الفحص الذكي لمناقصات الأعمال الإنشائية...")

    state    = load_state()
    seen_ids = set(state.get("seen_ids", []))
    all_new  = []

    # Libya Tenders
    log("📡 فحص: Libya Tenders (authenticated API)")
    lt_pairs = scrape_libyantenders()
    log(f"   → وجدت {len(lt_pairs)} مناقصة للتحليل")

    analyzer = CivilWorkAnalyzer()
    for title, href, sector_hint in lt_pairs:
        tid = make_id(href)
        if tid in seen_ids:
            continue
        seen_ids.add(tid)

        result = analyzer.analyze(title, "")
        if result is None:
            continue

        tender_dict = {
            "id":           tid,
            "title":        title[:220],
            "link":         href,
            "source":       "Libya Tenders",
            "sector":       result["sector"],
            "sector_color": result["sector_color"],
            "sector_icon":  result["sector_icon"],
            "solutions":    result["solutions"],
            "value":        result["value"],
            "civil_score":  result["civil_score"],
            "found_at":     now_gmt2().strftime("%Y-%m-%d %H:%M GMT+2"),
        }
        all_new.append(tender_dict)

        contacts = extract_contacts(title, organization="", tender_title=title[:100], source="Libya Tenders")
        if contacts:
            save_contacts_to_csv(contacts)

    log(f"   → {sum(1 for t in all_new if t['source']=='Libya Tenders')} مناقصة أعمال إنشائية مؤهلة")
    time.sleep(1)

    # NOC, Attaat, UNDP
    for key, src in list(SOURCES.items())[1:]:
        tenders = scrape_source(src["url"], src["name"])
        for t in tenders:
            if t["id"] not in seen_ids:
                all_new.append(t)
                seen_ids.add(t["id"])
        time.sleep(1)

    # Save real results to file for email system to use
    if all_new:
        try:
            with open('/home/solutions/.openclaw/civil_monitor_results.json', 'w', encoding='utf-8') as f:
                json.dump(all_new, f, ensure_ascii=False, indent=2)
            log(f"💾 تم حفظ {len(all_new)} مناقصة إلى civil_monitor_results.json")
        except Exception as e:
            log(f"⚠️  خطأ حفظ النتائج: {e}")

    if not all_new:
        log("📭 لا مناقصات أعمال إنشائية جديدة")
    else:
        log(f"🎉 {len(all_new)} مناقصة مؤهلة — جاري الإرسال...")
        batch = CONFIG["batch_size"]
        batches = [all_new[i:i+batch] for i in range(0, len(all_new), batch)]
        total = len(batches)

        for idx, group in enumerate(batches, 1):
            part = f" ({idx}/{total})" if total > 1 else ""
            subject = f"🏗️ Alwadi Civil Work Tenders{part} — {len(group)} مناقصة | {now_gmt2().strftime('%Y-%m-%d')}"
            html = build_email(group, idx, total)
            send_email(subject, html)
            if total > 1:
                time.sleep(3)

        # تحديث قائمة المناقصات المرسلة
        state["civil_tenders"] = [t["id"] for t in all_new]

        state["seen_ids"] = list(seen_ids)[-400:]

    state["last_check"] = now_gmt2().isoformat() + " GMT+2"
    save_state(state)
    log("✅ انتهى الفحص")
    log("=" * 55)

if __name__ == "__main__":
    main()
