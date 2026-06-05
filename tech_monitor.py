#!/usr/bin/env python3
"""
Alwadi Tech Monitor — IT/Telecom Specialist
يحلل مناقصات الاتصالات وتقنية المعلومات فقط
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
    print("⚠️ Libya filter not available")

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
    "from_name":  "Alwadi Tech Monitor",
    "batch_size": 10,
    "libyantenders_email": "sales@alwadi.ly",
    "libyantenders_pass":  "Alwadi-Coms*2005",
    "production_mode": True,
    "version": "2.0 Enhanced",
}

SOURCES = {
    "libyantenders": {"url": "https://libyantenders.ly/",         "name": "Libya Tenders"},
    "noc":           {"url": "https://noc.ly/tenders/",           "name": "NOC"},
    "attaat":        {"url": "https://www.attaat.pm.gov.ly/",     "name": "Attaat"},
    "ungm":          {"url": "https://www.ungm.org/",             "name": "UNGM Procurement"},
}

STATE_FILE = "/home/solutions/.openclaw/tech_state.json"
LOG_FILE   = "/home/solutions/.openclaw/tech_cron.log"
CONTACTS_FILE = "/home/solutions/.openclaw/tech_contacts.csv"
HEADERS    = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/120",
              "Accept-Language": "ar,en;q=0.9"}

# ======================== CONTEXTUAL ANALYZER =========================

class TechAnalyzer:
    """
    محلل متخصص بـ IT/Telecom — يقرأ النص الكامل للمناقصة ويحكم:
    1. هل هي مشروع IT/Telecom فعلاً؟
    2. ما نوع الحل التقني؟
    3. ما الحل المقترح؟
    4. ما القيمة المقدرة؟
    """

    # --- IT positive signals (context-aware) ---
    IT_SIGNALS = {
        "شبكة بيانات":      8, "شبكات بيانات":    8, "بنية تحتية تقنية": 9,
        "معدات شبكات":      9, "معدات اتصالات":   9, "شبكات اتصالات":    9,
        "نظام معلومات":     8, "أنظمة معلومات":   8, "تقنية المعلومات":  9,
        "أمن معلومات":      9, "أمن سيبراني":     9, "حماية بيانات":     8,
        "سلامة معلومات":     8, "سلامة بيانات":    8, "اتصالات":          7,
        "معدات":            4, "شبكات":           6,
        "نسخ احتياطي":      8, "استرداد بيانات":  8, "حل تخزين":         7,
        "خادم":             6, "سيرفر":           6, "خوادم":            6,
        "مركز بيانات":      9, "data center":      9, "datacenter":        9,
        "جدار ناري":        9, "firewall":         9, "antivirus":         8,
        "برمجيات":          6, "برامج":           8, "برنامج":           8, "software":         6, "تطبيق إلكتروني":   7,
        "office software":   8, "office suite":    8, "مجموعة برامج":     8,
        "تشغيل":            7, "نظام تشغيل":      9, "operating system":  9,
        "نظام إدارة":       6, "erp":             8, "crm":              7,
        "كاميرا ip":        7, "كاميرات مراقبة":    8, "كاميرا مراقبة":   8,
        "ip camera":        8, "cctv":             7, "كاميرا":           5, "كاميرات":         5,
        "نظام مراقبة":      5, "مراقبة":           5,
        "ألياف ضوئية":      8, "fiber optic":      8, "structured cabling": 8,
        "واي فاي":          7, "wifi":             7, "wi-fi":            7,
        "voip":             8, "ip phone":         8, "pbx":              7,
        "سنترال":           5, "هاتف ip":          7,
        "ups":              6, "طاقة غير منقطعة":  6,
        "switch":           7, "router":           7, "access point":      7,
        "laptop":           6, "كمبيوتر":          5, "حاسوب":            5,
        "طابعة":            4, "printer":          4, "ماسح ضوئي":        4,
        "cloud":            7, "سحابي":            7, "virtualization":    8,
        "cybersecurity":    9, "cyber security":   9, "أمن الشبات":        9,
        "endpoint":         7, "endpoint protection": 8, "siem":             9,
        "penetration testing": 8, "vulnerability assessment": 8, "incident response": 8,
        "اختبار الاختراق":    8, "تقييم الثغرات":   8, "الاستجابة للحوادث": 8,
        "intrusion detection": 8, "ids":           8, "ips":             8,
        "دفاع سيبراني":       9, "حماية سيبرانية":   9, "أمن سيبراني متقدم": 9,
        "threat detection":  8, "malware":         8, "ddos protection": 8,
        "network":          6, "لاسلكي":           6, "wireless":         6,
        "smart system":     7, "نظام ذكي":         7, "أتمتة":            6,
        "ict":              8, "it solution":      8, "it infrastructure": 9,
        "لابتوب":           6, "جهاز لوحي":        5, "tablet":           5,
        "digital":          5, "رقمي":             5, "تحول رقمي":        8,
        "ترخيص":            6, "license":          6, "ترخيص برمجيات":   8,
        "تطوير":            5, "development":      5, "تطوير نظم":        8,
        "supplier registration": 8, "vendor qualification": 8, "register supplier": 8,
        "تسجيل موردين":      8, "قائمة الموردين":     7, "تسجيل كمورد":      8,
        "اعتماد موردين":      7, "موردين معتمدين":    7, "تصنيف موردين":    7,
        "supplier accreditation": 7, "vendor approval": 7, "supplier list": 7,
        "suppliers list":    7, "qualified suppliers": 7,
        "windows":          8, "microsoft":         7,
        "cisco":            8, "switch":            7, "router":           7,
        "catalyst":         7, "transceivers":      7,
        "ملف مرفق":         3,
    }

    # --- Strong non-IT signals that kill IT classification ---
    NOT_IT_SIGNALS = {
        "office equipment": -8, "مستلزمات مكاتب": -8, "مستلزمات": -6,
        "supplies": -5, "أثاث": -9, "furniture": -9,
        "حراسة أمنية": -10, "خدمات حراسة": -10, "حارس أمن": -10,
        "security guard": -10, "bodyguard": -10, "guard service": -10,
        "أفراد أمن": -10, "كاميرا مراقبة بشرية": -8,
        "أعمال إنشائية": -9, "مقاولات": -8, "خرسانة": -9,
        "construction": -8, "civil works": -9, "أعمال مدنية": -9,
        "بناء": -7, "طريق": -8, "جسر": -9, "حفر": -6,
        "مركبات": -9, "سيارات": -9, "vehicles": -9, "trucks": -8,
        "نقل بري": -8, "transportation": -7,
        "مواد غذائية": -9, "تموين": -8, "وجبات": -9, "food": -8,
        "catering": -9, "مطعم": -8,
        "أدوية": -9, "دواء": -9, "معدات طبية": -6, "أجهزة طبية": -6,
        "medical equipment": -6, "مستلزمات طبية": -7,
        "جراحة": -9, "عملية طبية": -9,
        "تنظيف": -6, "cleaning": -6, "نفايات": -7, "waste": -7,
        "صيانة مكتب": -9, "صيانة": -6, "maintenance": -5,
        "إسمنت": -9, "حديد": -8, "steel": -8, "cement": -9,
        "وقود": -7, "fuel": -7, "diesel": -7, "بنزين": -7,
        "solar": -9, "شمسي": -9, "طاقة شمسية": -9,
    }

    # --- Solution mapping (context-aware) ---
    SOLUTIONS = [
        {
            "triggers":   ["نسخ احتياطي", "backup", "استرداد", "recovery", "commvault", "veeam", "arcserve"],
            "solution":   "Backup & Recovery — Veeam / Commvault / Arcserve",
            "value_range": (80_000, 500_000),
        },
        {
            "triggers":   ["أمن سيبراني", "أمن معلومات", "cybersecurity", "firewall", "جدار ناري",
                           "siem", "soc", "endpoint", "antivirus", "penetration", "اختبار اختراق"],
            "solution":   "Cybersecurity — Fortinet / Palo Alto / Rapid7 / CrowdStrike",
            "value_range": (50_000, 1_000_000),
        },
        {
            "triggers":   ["شبكة", "network", "switch", "router", "access point", "lan", "wan",
                           "sd-wan", "ألياف ضوئية", "fiber", "cabling", "كابلات"],
            "solution":   "Network Infrastructure — Cisco / Huawei / HPE Aruba",
            "value_range": (30_000, 800_000),
        },
        {
            "triggers":   ["خادم", "server", "rack", "blade", "hci", "hyper-converged"],
            "solution":   "Servers & Infrastructure — HPE / Dell / Lenovo",
            "value_range": (50_000, 1_500_000),
        },
        {
            "triggers":   ["تخزين", "storage", "san", "nas", "all-flash", "hybrid storage"],
            "solution":   "Storage Solutions — NetApp / Pure Storage / HPE",
            "value_range": (80_000, 2_000_000),
        },
        {
            "triggers":   ["مركز بيانات", "data center", "datacenter", "غرفة خوادم"],
            "solution":   "Data Center Build-Out — Full infrastructure + cooling + power",
            "value_range": (500_000, 10_000_000),
        },
        {
            "triggers":   ["سحاب", "cloud", "azure", "aws", "hybrid cloud", "private cloud"],
            "solution":   "Cloud Solutions — Azure / AWS / VMware",
            "value_range": (30_000, 500_000),
        },
        {
            "triggers":   ["cctv", "ip camera", "كاميرا ip", "نظام مراقبة", "nvr", "vms"],
            "solution":   "IP Surveillance — Hikvision / Axis / Milestone VMS",
            "value_range": (20_000, 300_000),
        },
        {
            "triggers":   ["erp", "نظام إدارة موارد", "نظام مالي", "نظام محاسبة",
                           "hr system", "نظام موارد بشرية"],
            "solution":   "ERP / Business Systems — Oracle / SAP / Microsoft Dynamics",
            "value_range": (100_000, 2_000_000),
        },
        {
            "triggers":   ["واي فاي", "wifi", "wi-fi", "لاسلكي", "wireless"],
            "solution":   "Wireless Networking — Cisco / HPE Aruba / Ubiquiti",
            "value_range": (15_000, 200_000),
        },
        {
            "triggers":   ["voip", "ip phone", "sip", "pbx", "سنترال", "هاتف ip", "unified comms"],
            "solution":   "IP Telephony — Cisco / Yealink / 3CX",
            "value_range": (10_000, 150_000),
        },
        {
            "triggers":   ["ups", "طاقة غير منقطعة", "power protection", "apc", "eaton"],
            "solution":   "Power Protection — APC / Eaton / Vertiv",
            "value_range": (10_000, 200_000),
        },
        {
            "triggers":   ["laptop", "لابتوب", "كمبيوتر", "desktop", "tablet", "جهاز لوحي",
                           "طابعة", "printer", "scanner"],
            "solution":   "End-User Devices — HP / Dell / Lenovo",
            "value_range": (5_000, 200_000),
        },
        {
            "triggers":   ["تحول رقمي", "digital transformation", "smart city", "مدينة ذكية",
                           "منصة إلكترونية", "بوابة", "portal", "تطبيق"],
            "solution":   "Digital Transformation / Software Development",
            "value_range": (50_000, 3_000_000),
        },
    ]

    def analyze(self, title: str, full_text: str) -> dict | None:
        """يحلل المناقصة ويرجع dict أو None"""
        combined = (title + " " + full_text).lower()
        combined_orig = title + " " + full_text

        # --- 1. فلتر ليبيا ---
        if LIBYA_FILTER_ENABLED:
            if not libya_filter.is_libya_related(title + " " + full_text):
                return None

        # --- 2. حساب نقاط IT ---
        it_score = 0
        for kw, score in self.IT_SIGNALS.items():
            if kw.lower() in combined:
                it_score += score

        # --- 3. خصم نقاط non-IT ---
        not_it_score = 0
        for kw, penalty in self.NOT_IT_SIGNALS.items():
            if kw.lower() in combined:
                not_it_score += penalty

        net_score = it_score + not_it_score

        # رفض إذا النتيجة غير كافية
        threshold = 3 if full_text.strip() else 2
        if net_score < threshold:
            return None

        # --- 4. اقتراح الحل ---
        matched_solutions = []

        for rule in self.SOLUTIONS:
            if any(t.lower() in combined for t in rule["triggers"]):
                matched_solutions.append(rule["solution"])

        if not matched_solutions:
            matched_solutions = ["حل IT متكامل — يحتاج دراسة تفصيلية"]

        # --- 5. استخراج أو تقدير القيمة ---
        extracted_value = self._extract_value(combined_orig)
        if extracted_value:
            value_str = extracted_value
        else:
            estimated, reason = self._estimate_value_from_description(combined_orig)
            value_str = f"${self._fmt(estimated)} (تقدير: {reason})"

        return {
            "solutions": matched_solutions[:2],
            "value":     value_str,
            "it_score":  net_score,
        }

    def _extract_value(self, text: str) -> str:
        """استخراج القيمة المذكورة وتطبيق خصم 35%"""
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
                    final = int(value_num * 0.65)
                    return f"${self._fmt(final)} (المذكورة: {value_text} × 0.65)"
                return value_text
        return ""

    def _parse_value_number(self, text: str, match) -> int | None:
        """استخراج الرقم من القيمة المذكورة"""
        try:
            full_match = match.group(0)
            numbers = re.findall(r'\d+(?:[,\.]\d+)*', full_match)
            if not numbers:
                return None

            num_str = numbers[0].replace(',', '')
            if '.' in num_str:
                num = int(float(num_str))
            else:
                num = int(num_str)

            if 'مليون' in full_match.lower() or 'million' in full_match.lower():
                num *= 1_000_000
            elif 'ألف' in full_match.lower() or 'thousand' in full_match.lower():
                num *= 1_000

            return num
        except:
            return None

    def _estimate_value_from_description(self, text: str) -> tuple[int, str]:
        """تقدير الشغل والأجهزة للـ IT ثم خفض 35%"""
        components = ['خادم', 'switch', 'router', 'كاميرا', 'جهاز', 'لابتوب', 'حاسوب', 'معدات', 'ترخيص', 'firewall']
        component_count = sum(1 for c in components if c.lower() in text.lower())

        is_enterprise = any(w in text.lower() for w in ['مركز بيانات', 'data center', 'مشروع كبير', 'large scale'])
        is_mid = any(w in text.lower() for w in ['نظام', 'شبكة', 'infrastructure', 'security'])

        total_cost = 0

        if is_enterprise:
            total_cost = 1_200_000
        elif is_mid:
            total_cost = 600_000
        elif component_count >= 5:
            total_cost = 800_000
        elif component_count >= 3:
            total_cost = 450_000
        else:
            total_cost = 220_000

        final_value = int(total_cost * 0.65)
        reason = f"مكونات: {component_count} - خفض 35%"

        return final_value, reason

    def _fmt(self, n: int) -> str:
        if n >= 1_000_000:
            return f"{n/1_000_000:.1f}M"
        if n >= 1_000:
            return f"{n/1_000:.0f}K"
        return str(n)


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
        "tech_tenders": [],
        "last_check": None,
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

def fetch_page(url, timeout=12):
    try:
        r = requests.get(url, timeout=timeout, headers=HEADERS)
        r.raise_for_status()
        return r.text
    except Exception as e:
        log(f"  ⚠️  فشل جلب {url}: {e}")
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

def extract_pdf_text(pdf_bytes):
    if not HAS_PDF or not pdf_bytes:
        return ""
    try:
        if 'pdfplumber' in dir():
            import io
            with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf:
                text = ""
                for page in pdf.pages[:3]:
                    text += page.extract_text() or ""
                return clean_text(text[:2000])
        else:
            import io
            reader = PyPDF2.PdfReader(io.BytesIO(pdf_bytes))
            text = ""
            for page in reader.pages[:3]:
                text += page.extract_text() or ""
            return clean_text(text[:2000])
    except Exception as e:
        log(f"  ⚠️  فشل استخراج نص PDF: {e}")
        return ""

def fetch_file_text(url):
    if not url:
        return ""
    try:
        r = requests.get(url, timeout=5, headers=HEADERS, stream=True)
        if r.status_code == 200:
            return f" [ملف مرفق: {url[-50:]}]"
    except Exception as e:
        pass
    return ""

def find_attachment_links(soup, base_url):
    links = []
    for a in soup.find_all("a", href=True):
        href = a["href"].strip()
        if not href:
            continue
        if not href.startswith("http"):
            href = urljoin(base_url, href)
        if any(ext in href.lower() for ext in ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.txt']):
            links.append(href)
    return links

def extract_contacts(text, organization="", tender_title="", source=""):
    contacts = []
    email_pattern = r'\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b'
    emails = re.findall(email_pattern, text)
    phone_pattern = r'(?:\+218|00218|0)?(?:9[0-2]|2[1-3])\d{7,8}'
    phones = re.findall(phone_pattern, text)

    for email in set(emails):
        if email.lower() in ['test@test.com', 'example@example.com', 'contact@contact.com']:
            continue
        contact = {
            'date_found': now_gmt2().strftime("%Y-%m-%d"),
            'organization': organization,
            'contact_name': '',
            'job_title': '',
            'email': email,
            'phone': '',
            'tender_title': tender_title,
            'sector': 'IT/Telecom',
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
            'sector': 'IT/Telecom',
            'source': source
        }
        contacts.append(contact)

    return contacts

def save_contacts_to_csv(contacts):
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
    s = requests.Session()
    s.headers.update({"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/120"})

    try:
        r = s.get("https://libyantenders.ly/tds/login", timeout=15)
        soup = BeautifulSoup(r.text, "html.parser")
        csrf = soup.find("meta", {"name": "csrf-token"}).get("content", "")
        s.post("https://libyantenders.ly/tds/login",
               data={"_token": csrf,
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
                parent_text = re.sub(
                    r'(عرض التفاصيل|عرض|view|details|تفاصيل|العنوان\s*:?|الموعد النهائي\s*:?)',
                    ' ', parent_text, flags=re.IGNORECASE
                )
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
    analyzer = TechAnalyzer()
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

            try:
                soup = BeautifulSoup(detail_text, "html.parser")
                attachments = find_attachment_links(soup, href)[:1]
                for att_url in attachments:
                    file_text = fetch_file_text(att_url)
                    if file_text:
                        detail_text += file_text
            except:
                pass

        result = analyzer.analyze(title, detail_text)
        if result is None:
            continue

        tender_dict = {
            "id":           tid,
            "title":        title[:220],
            "link":         href,
            "source":       src_name,
            "sector":       "IT/Telecom",
            "sector_color": "#0000FF",
            "sector_icon":  "📡",
            "solutions":    result["solutions"],
            "value":        result["value"],
            "it_score":     result["it_score"],
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

    results.sort(key=lambda x: x["it_score"], reverse=True)
    log(f"   → {len(results)} مناقصة IT/Telecom مؤهلة")
    return results[:20]

# ======================== EMAIL =========================

def build_email(tenders, part_num=None, total_parts=None):
    sector_color = "#0000FF"
    sector_name = "IT/Telecom"
    part_label = f" — الجزء {part_num}/{total_parts}" if total_parts and total_parts > 1 else ""
    rows = ""
    for i, t in enumerate(tenders, 1):
        bg = "#f7f9fc" if i % 2 == 0 else "white"
        sol_html = "<br>".join(
            f"<span style='color:{sector_color}'>• {s}</span>" for s in t["solutions"]
        )
        rows += f"""
        <tr style="background:{bg};vertical-align:top">
          <td style="padding:11px 8px;border:1px solid #dde;text-align:center;
                     color:#999;font-size:12px;width:26px">{i}</td>
          <td style="padding:11px 10px;border:1px solid #dde;line-height:1.7">
            <div style="font-size:14px;font-weight:bold">{t['title']}</div>
            <div style="margin-top:5px">
              <span style="background:{sector_color};color:white;padding:2px 8px;
                           border-radius:3px;font-size:11px">{t['sector_icon']} {t['sector']}</span>
              &nbsp;<span style="color:#999;font-size:11px">{t['source']} | {t['found_at']}</span>
            </div>
          </td>
          <td style="padding:11px 10px;border:1px solid #dde;font-size:12px;
                     line-height:1.8;min-width:190px">{sol_html}</td>
          <td style="padding:11px 10px;border:1px solid #dde;font-size:12px;
                     min-width:130px;color:{sector_color}">
            <strong>{t['value']}</strong>
          </td>
          <td style="padding:11px 8px;border:1px solid #dde;text-align:center;width:55px">
            <a href="{t['link']}" style="background:{sector_color};color:white;padding:5px 10px;
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
              <div class="card-badge" style="background:{sector_color}">{t['sector_icon']} {t['sector']}</div>
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
      .tender-card {{
        background: white;
        border: 1px solid #dde;
        border-radius: 8px;
        padding: 12px;
        margin-bottom: 12px;
        box-shadow: 0 1px 3px rgba(0,0,0,0.1);
      }}
      .card-title {{font-size: 13px; font-weight: bold; margin-bottom: 8px; line-height: 1.4;}}
      .card-badge {{
        display: inline-block;
        padding: 3px 8px;
        border-radius: 3px;
        color: white;
        font-size: 10px;
        margin-bottom: 8px;
      }}
      .card-meta {{color: #999; font-size: 10px; margin-bottom: 8px;}}
      .card-solution {{color: {sector_color}; font-size: 11px; line-height: 1.6; margin-bottom: 8px;}}
      .card-value {{color: {sector_color}; font-weight: bold; margin-bottom: 8px; font-size: 12px;}}
      .card-button {{
        display: inline-block;
        background: {sector_color};
        color: white;
        padding: 6px 12px;
        border-radius: 4px;
        text-decoration: none;
        font-size: 11px;
        width: 100%;
        text-align: center;
        box-sizing: border-box;
      }}
    }}
    @media (min-width: 641px) {{
      .tender-cards {{display: none;}}
    }}
  </style>
</head>
<body style="font-family:Tahoma,Arial,sans-serif;direction:rtl;background:#eef1f5;padding:16px;margin:0">
  <div style="max-width:900px;margin:auto;background:white;border-radius:10px;
              overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.13)">
    <div style="background:{sector_color};color:white;padding:18px 22px">
      <h2 style="margin:0;font-size:17px">
        مناقصات {sector_name} — Alwadi Monitor{part_label}
      </h2>
      <p style="margin:6px 0 0;font-size:12px;opacity:0.8">
        {now_gmt2().strftime('%Y-%m-%d %H:%M GMT+2')} &nbsp;|&nbsp;
        عدد المناقصات: {len(tenders)} &nbsp;|&nbsp;
        المصادر: Libya Tenders · NOC · Attaat
      </p>
    </div>
    <div style="padding:18px 20px">
      <p style="margin-top:0;font-size:14px">
        السلام عليكم Team،<br>
        فيما يلي المناقصات الجديدة المؤهلة في قطاع <strong>{sector_name}</strong>.
      </p>

      <!-- Desktop Table View -->
      <table class="tender-table" style="width:100%;border-collapse:collapse">
        <thead>
          <tr style="background:{sector_color};color:white;font-size:12px">
            <th style="padding:10px 8px;border:1px solid #aab">#</th>
            <th style="padding:10px;border:1px solid #aab;text-align:right">المناقصة</th>
            <th style="padding:10px;border:1px solid #aab;text-align:right">الحل المقترح</th>
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
        Alwadi Communications · تحليل تلقائي · يُرجى التحقق من الموقع للتفاصيل الكاملة
      </p>
    </div>
  </div>
</body>
</html>"""

# ======================== SEND =========================

def send_email(subject, html_body):
    plain = f"Alwadi Tech Monitor\n{subject}\n\nيرجى قراءة هذا الإيميل بتنسيق HTML.\n\nAlwadi Communications"
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
    """Send notification when no new tenders found"""
    sector_color = "#0000FF"
    sector_icon = "📡"
    sector = "IT/Telecom"
    subject = f"No New Tenders — {sector} | {now_gmt2().strftime('%Y-%m-%d')}"
    html_body = f"""
<html>
<head>
  <meta charset="utf-8">
</head>
<body style="font-family:Tahoma,Arial,sans-serif;direction:rtl;background:#eef1f5;padding:16px;margin:0">
  <div style="max-width:600px;margin:auto;background:white;border-radius:10px;
              overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.13)">
    <div style="background:{sector_color};color:white;padding:18px 22px">
      <h2 style="margin:0;font-size:17px">
        {sector_icon} {sector} — بدون مناقصات جديدة
      </h2>
      <p style="margin:6px 0 0;font-size:12px;opacity:0.8">
        {now_gmt2().strftime('%Y-%m-%d %H:%M GMT+2')}
      </p>
    </div>
    <div style="padding:18px 20px">
      <p style="margin-top:0;font-size:14px;color:#666">
        السلام عليكم Team،<br><br>
        لم يتم العثور على مناقصات جديدة في قطاع <strong>{sector}</strong> خلال آخر فحص.
      </p>
      <p style="color:#999;font-size:12px;border-top:1px solid #eee;padding-top:10px">
        Alwadi Communications · مراقبة المناقصات التلقائية
      </p>
    </div>
  </div>
</body>
</html>"""
    return send_email(subject, html_body)

# ======================== MAIN =========================

def main():
    log("=" * 55)
    log("🔍 بدء الفحص الذكي لمناقصات IT/Telecom...")

    state    = load_state()
    seen_ids = set(state.get("seen_ids", []))
    all_new  = []

    # Libya Tenders — authenticated API
    log("📡 فحص: Libya Tenders (authenticated API)")
    lt_pairs = scrape_libyantenders()
    log(f"   → وجدت {len(lt_pairs)} مناقصة للتحليل")

    analyzer = TechAnalyzer()
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
            "sector":       "IT/Telecom",
            "sector_color": "#0000FF",
            "sector_icon":  "📡",
            "solutions":    result["solutions"],
            "value":        result["value"],
            "it_score":     result["it_score"],
            "found_at":     now_gmt2().strftime("%Y-%m-%d %H:%M GMT+2"),
        }
        all_new.append(tender_dict)

        contacts = extract_contacts(
            title,
            organization="",
            tender_title=title[:100],
            source="Libya Tenders"
        )
        if contacts:
            save_contacts_to_csv(contacts)
    log(f"   → {len(all_new)} مناقصة مؤهلة من Libya Tenders")
    time.sleep(1)

    # NOC, Attaat, UNGM — web scraping
    for key, src in list(SOURCES.items())[1:]:
        tenders = scrape_source(src["url"], src["name"])
        for t in tenders:
            if t["id"] not in seen_ids:
                all_new.append(t)
                seen_ids.add(t["id"])
        time.sleep(1)

    # Send email with all new tenders
    if all_new:
        log(f"📧 إرسال {len(all_new)} مناقصة IT/Telecom...")
        batch = CONFIG["batch_size"]
        batches = [all_new[i:i+batch] for i in range(0, len(all_new), batch)]
        total = len(batches)

        for idx, group in enumerate(batches, 1):
            part = f" ({idx}/{total})" if total > 1 else ""
            subject = f"📡 IT/Telecom Tenders{part} — {len(group)} مناقصة | {now_gmt2().strftime('%Y-%m-%d')}"
            html = build_email(group, idx, total)
            send_email(subject, html)
            if total > 1:
                time.sleep(2)

        state["tech_tenders"] = [t["id"] for t in all_new]
    else:
        log(f"📭 لا مناقصات جديدة")

    state["seen_ids"] = list(seen_ids)[-400:]
    state["last_check"] = now_gmt2().isoformat() + " GMT+2"
    save_state(state)
    log("✅ انتهى الفحص")
    log("=" * 55)

if __name__ == "__main__":
    main()
