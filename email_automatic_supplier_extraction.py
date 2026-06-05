#!/usr/bin/env python3
"""
AUTOMATIC SUPPLIER EXTRACTION MODULE - Email Intelligence v1.1 Enhancement
Automatically detects, extracts, and stores supplier information from incoming emails
"""

import json
import os
import re
import hashlib
import smtplib
import urllib.request
import urllib.parse
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Any
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart


class AutomaticSupplierExtractor:
    """
    Automatically extracts supplier information from emails and stores in knowledge base.
    Detects supplier emails using contextual keywords and structured data extraction.
    """

    # Keywords that signal a supplier/vendor email
    SUPPLIER_KEYWORDS = {
        'offer', 'quotation', 'quote', 'price list', 'pricing', 'invoice',
        'supplier', 'vendor', 'distributor', 'manufacturer', 'product',
        'service', 'proposal', 'rfq', 'product sheet', 'datasheet',
        'specs', 'specification', 'technical data', 'brochure', 'catalog',
        'availability', 'stock', 'delivery', 'shipment', 'logistics',
        'warranty', 'support', 'technical support', 'contact us',
        'partnership', 'collaboration', 'wholesale', 'retail',
        'export', 'import', 'reseller', 'agent', 'representative'
    }

    # Contact field patterns
    CONTACT_PATTERNS = {
        'email': r'[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}',
        'phone': r'\+?[\d\s\-().]{10,}',
        'website': r'(?:https?://)?(?:www\.)?([a-zA-Z0-9-]+\.[a-zA-Z]{2,})',
        'address': r'(?:Address|Headquarters|Location|Based in|City)[:\s]+([^,\n]+(?:,\s*[^,\n]+)*)',
        'company': r'(?:^|\n)(?:Company|Firm|Organization|Manufacturer|Supplier)[:\s]+([^\n]+)',
        'contact_person': r'(?:Contact|Person|Manager|Director|CEO|Contact Person)[:\s]+([^\n]+)',
        'from_company': r'^From:\s*([^<\n]+?)\s*<[^>]+>'
    }

    # Product categories to help identify supplier type
    PRODUCT_CATEGORIES = {
        'infrastructure': ['server', 'cabinet', 'rack', 'enclosure', 'pdu', 'power distribution'],
        'power': ['ups', 'uninterruptible power supply', 'battery', 'backup', 'power management'],
        'networking': ['router', 'switch', 'firewall', 'network', 'cabling', 'fiber', 'copper'],
        'storage': ['storage', 'san', 'nas', 'backup', 'archive', 'nvme', 'ssd'],
        'computing': ['server', 'cpu', 'gpu', 'memory', 'disk', 'compute', 'processor'],
        'telecom': ['telecom', 'voip', 'pbx', 'phone', 'communication', 'carrier'],
        'security': ['firewall', 'vpn', 'security', 'access control', 'encryption', 'ssl'],
        'solar': ['solar', 'pv', 'photovoltaic', 'renewable', 'energy', 'panel'],
        'surveillance': ['cctv', 'camera', 'surveillance', 'video', 'security camera'],
    }

    # List of known distributors that ship to Libya
    LIBYA_DISTRIBUTORS = [
        'redington', 'absis', 'nexthop', 'synnex', 'd-link',
        'ingram', 'tech data', 'arrow electronics', 'distribution',
    ]

    # Distributor mapping by product category
    DISTRIBUTOR_BY_CATEGORY = {
        'networking': 'Redington',      # Telecom/networking equipment
        'telecom': 'Redington',          # Telecom products
        'infrastructure': 'Redington',   # Server/infrastructure
        'power': 'Redington',            # UPS and power equipment
        'storage': 'Redington',          # Storage solutions
    }

    def __init__(self, workspace_root: str = "/home/solutions/.openclaw/workspace"):
        self.workspace = workspace_root
        self.kb_path = os.path.join(workspace_root, "knowledge_base")
        self.suppliers_path = os.path.join(self.kb_path, "suppliers")
        self.suppliers_index_file = os.path.join(self.kb_path, "suppliers_index.json")
        self.extraction_log_file = os.path.join(self.kb_path, "supplier_extraction_log.json")

        self._initialize_directories()
        self.suppliers_index = self._load_suppliers_index()
        self.extraction_log = self._load_extraction_log()

    def _initialize_directories(self):
        """Create necessary directory structure"""
        os.makedirs(self.suppliers_path, exist_ok=True)
        os.makedirs(self.kb_path, exist_ok=True)

    def _load_suppliers_index(self) -> Dict:
        """Load existing suppliers index"""
        default_index = {
            "suppliers": [],
            "by_company": {},
            "by_contact_email": {},
            "by_product": {},
            "last_updated": datetime.utcnow().isoformat()
        }

        if os.path.exists(self.suppliers_index_file):
            try:
                with open(self.suppliers_index_file, 'r', encoding='utf-8') as f:
                    loaded = json.load(f)
                    # Ensure all required keys exist
                    for key in default_index:
                        if key not in loaded:
                            loaded[key] = default_index[key]
                    return loaded
            except:
                return default_index
        return default_index

    def _load_extraction_log(self) -> Dict:
        """Load extraction log for tracking processed emails"""
        if os.path.exists(self.extraction_log_file):
            with open(self.extraction_log_file, 'r', encoding='utf-8') as f:
                return json.load(f)
        return {
            "processed_emails": [],
            "extraction_count": 0,
            "last_extraction": None,
            "duplicate_detected": 0
        }

    def _save_suppliers_index(self):
        """Save suppliers index to file"""
        self.suppliers_index["last_updated"] = datetime.utcnow().isoformat()
        with open(self.suppliers_index_file, 'w', encoding='utf-8') as f:
            json.dump(self.suppliers_index, f, indent=2, ensure_ascii=False)

    def _save_extraction_log(self):
        """Save extraction log"""
        with open(self.extraction_log_file, 'w', encoding='utf-8') as f:
            json.dump(self.extraction_log, f, indent=2, ensure_ascii=False)

    def is_supplier_email(self, subject: str, body: str) -> Tuple[bool, float]:
        """
        Determine if email is from a supplier/vendor.
        Returns (is_supplier, confidence_score)
        """
        combined_text = (subject + " " + body).lower()
        keyword_count = 0

        for keyword in self.SUPPLIER_KEYWORDS:
            keyword_count += combined_text.count(keyword)

        confidence = min(100, (keyword_count / 3) * 100)  # Normalize score
        is_supplier = confidence >= 30  # 30% threshold

        return is_supplier, confidence

    def extract_company_details(self, body: str) -> Dict[str, Any]:
        """Extract company details from email body"""
        details = {
            "company_name": None,
            "country": None,
            "city": None,
            "address": None,
            "website": None,
        }

        # Method 1: Extract company name from explicit field
        company_match = re.search(self.CONTACT_PATTERNS['company'], body, re.IGNORECASE)
        if company_match:
            details["company_name"] = company_match.group(1).strip()

        # Method 2: Try to extract from email address (most reliable)
        if not details["company_name"]:
            email_match = re.search(self.CONTACT_PATTERNS['email'], body)
            if email_match:
                email_addr = email_match.group(0)
                # Extract domain from email
                domain = email_addr.split('@')[1].split('.')[0]
                if domain and len(domain) > 2:
                    # Capitalize words (xcessmart -> Xcessmart)
                    details["company_name"] = domain.capitalize()

        # Method 3: Try to extract from "From:" line (if contains actual name)
        if not details["company_name"]:
            from_line_match = re.search(r'^From:\s*(.+?)[\s<]', body, re.IGNORECASE | re.MULTILINE)
            if from_line_match:
                potential_name = from_line_match.group(1).strip()
                # Only use if it looks like a real name (not just email or symbols)
                if potential_name and len(potential_name) > 2 and not potential_name.startswith(('@', '_', '-')):
                    details["company_name"] = potential_name

        # Extract website
        website_match = re.search(self.CONTACT_PATTERNS['website'], body, re.IGNORECASE)
        if website_match:
            details["website"] = website_match.group(0).strip()

        # Extract address/location
        address_match = re.search(self.CONTACT_PATTERNS['address'], body, re.IGNORECASE)
        if address_match:
            details["address"] = address_match.group(1).strip()

        # Try to extract country (often in address or separate)
        country_pattern = r'(?:Istanbul|Turkey|Egypt|Libya|UAE|USA|UK|Germany|China|India)[^\n]*'
        country_match = re.search(country_pattern, body, re.IGNORECASE)
        if country_match:
            location = country_match.group(0)
            if 'Turkey' in location or 'Istanbul' in location:
                details["country"] = "Turkey"
                if 'Istanbul' in location:
                    details["city"] = "Istanbul"
            elif 'Egypt' in location:
                details["country"] = "Egypt"
            elif 'Libya' in location:
                details["country"] = "Libya"
            elif 'UAE' in location or 'Dubai' in location or 'Abu Dhabi' in location:
                details["country"] = "UAE"
            elif 'USA' in location:
                details["country"] = "USA"

        return details

    def extract_contact_details(self, body: str) -> Dict[str, Any]:
        """Extract contact person details from email"""
        contacts = {
            "contact_person": None,
            "job_title": None,
            "email": None,
            "phone": None,
            "mobile": None,
            "whatsapp": None,
            "fax": None,
            "skype": None,
        }

        # Extract email addresses FIRST
        email_matches = re.findall(self.CONTACT_PATTERNS['email'], body)
        if email_matches:
            contacts["email"] = email_matches[0]  # Primary email
            # Extract person name from email username as default (febix -> Febix)
            email_username = email_matches[0].split('@')[0]
            contacts["contact_person"] = email_username.capitalize()

        # Extract contact person name from explicit field (override email extraction if valid)
        person_match = re.search(self.CONTACT_PATTERNS['contact_person'], body, re.IGNORECASE)
        if person_match:
            name_extracted = person_match.group(1).strip()
            # Clean up extracted name (remove "Person:" prefix if exists)
            name_extracted = re.sub(r'^(?:Person|Contact):\s*', '', name_extracted, flags=re.IGNORECASE).strip()
            # Only override if it looks like a real name (not too long, not empty)
            if len(name_extracted) > 1 and len(name_extracted) < 50:
                contacts["contact_person"] = name_extracted

        # Extract phone numbers
        phone_matches = re.findall(self.CONTACT_PATTERNS['phone'], body)
        if phone_matches:
            for phone in phone_matches:
                if '+90' in phone or '216' in phone:  # Turkey/Libya country codes
                    contacts["phone"] = phone
                elif not contacts["phone"]:
                    contacts["phone"] = phone

        # Extract mobile/WhatsApp if explicitly mentioned
        mobile_pattern = r'(?:Mobile|WhatsApp|Tel)[\s:]+(\+?[\d\s\-().]+)'
        mobile_matches = re.findall(mobile_pattern, body, re.IGNORECASE)
        if mobile_matches:
            contacts["mobile"] = mobile_matches[0]
            if 'whatsapp' in body.lower() and '+' in mobile_matches[0]:
                contacts["whatsapp"] = mobile_matches[0]

        # Extract job title
        title_pattern = r'(?:Title|Position|Role)[\s:]+([^\n]+)'
        title_match = re.search(title_pattern, body, re.IGNORECASE)
        if title_match:
            contacts["job_title"] = title_match.group(1).strip()

        # Extract Skype if present
        skype_pattern = r'(?:Skype|skype)[\s:]+([a-zA-Z0-9._-]+)'
        skype_match = re.search(skype_pattern, body)
        if skype_match:
            contacts["skype"] = skype_match.group(1)

        return contacts

    def detect_libya_distributor(self, body: str, product_categories: List[str] = None) -> Optional[str]:
        """
        Detect if supplier mentions being a distributor or suggest distributor based on products.
        Returns the distributor name that ships to Libya.
        """
        body_lower = body.lower()

        # First, check if supplier explicitly mentions any known distributors
        for distributor in self.LIBYA_DISTRIBUTORS:
            if distributor in body_lower:
                return distributor.capitalize()

        # If we have product categories, suggest distributor based on product type
        if product_categories:
            for category in product_categories:
                category_lower = category.lower().strip()
                # Check if this category maps to a known distributor
                for mapped_category, distributor in self.DISTRIBUTOR_BY_CATEGORY.items():
                    if mapped_category in category_lower or category_lower in mapped_category:
                        return f"{distributor} (Libya Distributor)"

        # Check for general distributor mentions
        if any(word in body_lower for word in ['distributor', 'distribution', 'authorized distributor', 'official distributor']):
            return "Distributor (Libya)"

        return None

    def extract_products(self, body: str) -> Dict[str, List[str]]:
        """Identify products offered by supplier"""
        products = {
            "categories": [],
            "mentioned_products": [],
            "specs_provided": False
        }

        body_lower = body.lower()

        # Check for product categories
        for category, keywords in self.PRODUCT_CATEGORIES.items():
            for keyword in keywords:
                if keyword in body_lower:
                    if category not in products["categories"]:
                        products["categories"].append(category)

        # Extract specific product mentions using multiple patterns
        product_patterns = [
            r'(?:Products|Services|Offers|Available|Provides)[\s:]*([^\n]+)',
            r'(?:We offer|We provide|Our products|Supplying)[\s:]*([^\n]+)',
            r'(?:product|service)[\s:]*([^\n]*(?:system|device|solution|equipment|software|unit)[^\n]*)',
            r'(?:includes|includes:|includes\s*)([^\n]*(?:-|•|◦)[^\n]*)',  # Bulleted lists
            r'^-\s+(.+?)$',  # Bullet points with dash
        ]

        for pattern in product_patterns:
            product_matches = re.findall(pattern, body, re.IGNORECASE | re.MULTILINE)
            for match in product_matches:
                match_clean = match.strip()
                if match_clean and len(match_clean) > 2 and match_clean not in products["mentioned_products"]:
                    products["mentioned_products"].append(match_clean)

        # Check if specs/datasheets are provided
        if re.search(r'(?:specification|datasheet|technical|brochure|catalog)', body, re.IGNORECASE):
            products["specs_provided"] = True

        return products

    def extract_commercial_terms(self, body: str) -> Dict[str, Any]:
        """Extract commercial and pricing information"""
        terms = {
            "pricing_info": None,
            "payment_terms": None,
            "delivery_terms": None,
            "moq": None,  # Minimum Order Quantity
            "discounts": None,
            "warranty": None,
        }

        body_lower = body.lower()

        # Extract pricing information
        price_pattern = r'(?:price|pricing|cost|rate)[\s:]*(\$?[\d,]+(?:\.\d{2})?(?:\s*(?:per|/|each))?[^\n]*)'
        price_match = re.search(price_pattern, body, re.IGNORECASE)
        if price_match:
            terms["pricing_info"] = price_match.group(1).strip()

        # Extract payment terms
        payment_pattern = r'(?:payment terms?|terms?|net)[\s:]*([^\n]+)'
        payment_match = re.search(payment_pattern, body, re.IGNORECASE)
        if payment_match:
            terms["payment_terms"] = payment_match.group(1).strip()

        # Extract delivery/shipping terms
        delivery_pattern = r'(?:delivery|shipping|fob|cif|dap)[\s:]*([^\n]+)'
        delivery_match = re.search(delivery_pattern, body, re.IGNORECASE)
        if delivery_match:
            terms["delivery_terms"] = delivery_match.group(1).strip()

        # Extract MOQ
        moq_pattern = r'(?:minimum order|moq)[\s:]*(\d+[^\n]*)'
        moq_match = re.search(moq_pattern, body, re.IGNORECASE)
        if moq_match:
            terms["moq"] = moq_match.group(1).strip()

        # Extract warranty information
        warranty_pattern = r'(?:warranty|guarantee|coverage)[\s:]*([^\n]+)'
        warranty_match = re.search(warranty_pattern, body, re.IGNORECASE)
        if warranty_match:
            terms["warranty"] = warranty_match.group(1).strip()

        # Check for discount tiers
        if re.search(r'(?:discount|bulk|volume|tier)', body, re.IGNORECASE):
            terms["discounts"] = "Available (see details)"

        return terms

    def generate_supplier_id(self, company_name: str, contact_email: str) -> str:
        """Generate unique supplier ID (simplified - company name only)"""
        if not company_name:
            company_name = "Unknown"

        # Use company name as the supplier ID (simplified format)
        # Format: SUP_CompanyName
        supplier_id = f"SUP_{company_name.strip()}"
        return supplier_id

    def check_duplicate_supplier(self, company_name: str, contact_email: str) -> Tuple[bool, Optional[str]]:
        """Check if supplier already exists in database"""
        if not company_name or not contact_email:
            return False, None

        company_name_lower = company_name.lower().strip()
        email_lower = contact_email.lower().strip()

        # Check by company name
        if company_name_lower in self.suppliers_index.get("by_company", {}):
            existing_id = self.suppliers_index["by_company"][company_name_lower]
            return True, existing_id

        # Check by email
        if email_lower in self.suppliers_index.get("by_contact_email", {}):
            existing_id = self.suppliers_index["by_contact_email"][email_lower]
            return True, existing_id

        return False, None

    def store_supplier(self, supplier_data: Dict[str, Any], email_source: Optional[Dict] = None) -> Tuple[bool, str]:
        """
        Store supplier information in knowledge base.
        Returns (success, supplier_id_or_error_message)
        """
        company_name = supplier_data.get("company_details", {}).get("company_name")
        contact_email = supplier_data.get("contact_details", {}).get("email")

        # Validate minimum required data
        if not company_name:
            return False, "ERROR: Company name is required"

        # Check for duplicates
        is_duplicate, existing_id = self.check_duplicate_supplier(company_name, contact_email)
        if is_duplicate:
            self.extraction_log["duplicate_detected"] += 1
            self._save_extraction_log()
            return False, f"DUPLICATE: Supplier already exists as {existing_id}"

        # Generate supplier ID
        supplier_id = self.generate_supplier_id(company_name, contact_email or "unknown")

        # Create supplier record
        supplier_record = {
            "supplier_id": supplier_id,
            "company_details": supplier_data.get("company_details", {}),
            "contact_details": supplier_data.get("contact_details", {}),
            "products": supplier_data.get("products", {}),
            "commercial_terms": supplier_data.get("commercial_terms", {}),
            "metadata": {
                "extracted_at": datetime.utcnow().isoformat(),
                "extraction_confidence": supplier_data.get("confidence", 0),
                "source_email": email_source,
                "has_attachments": email_source.get("has_attachments", False) if email_source else False
            }
        }

        # Save to individual supplier file
        supplier_file = os.path.join(self.suppliers_path, f"{supplier_id}.json")
        with open(supplier_file, 'w', encoding='utf-8') as f:
            json.dump(supplier_record, f, indent=2, ensure_ascii=False)

        # Update index
        self.suppliers_index["suppliers"].append(supplier_id)
        if company_name:
            self.suppliers_index["by_company"][company_name.lower().strip()] = supplier_id
        if contact_email:
            self.suppliers_index["by_contact_email"][contact_email.lower().strip()] = supplier_id

        # Index by products
        for category in supplier_data.get("products", {}).get("categories", []):
            if category not in self.suppliers_index["by_product"]:
                self.suppliers_index["by_product"][category] = []
            self.suppliers_index["by_product"][category].append(supplier_id)

        self._save_suppliers_index()

        # Update extraction log
        self.extraction_log["extraction_count"] += 1
        self.extraction_log["last_extraction"] = datetime.utcnow().isoformat()
        self._save_extraction_log()

        return True, supplier_id

    def process_email(self, email_data: Dict[str, Any]) -> Dict[str, Any]:
        """
        Process an email for automatic supplier extraction.

        Args:
            email_data: Dict with keys: subject, body, from_address, date, email_id, has_attachments

        Returns:
            Processing result with status, supplier_id, and details
        """
        subject = email_data.get("subject", "")
        body = email_data.get("body", "")
        email_id = email_data.get("email_id", "unknown")
        from_address = email_data.get("from_address", "")

        result = {
            "email_id": email_id,
            "status": "processed",
            "is_supplier_email": False,
            "supplier_id": None,
            "message": "Email processed",
            "extraction_confidence": 0,
            "extracted_data": None
        }

        # Check if this is a supplier email
        is_supplier, confidence = self.is_supplier_email(subject, body)
        result["extraction_confidence"] = confidence

        if not is_supplier:
            result["status"] = "skipped"
            result["message"] = f"Not a supplier email (confidence: {confidence:.1f}%)"
            return result

        # Extract all supplier information
        extracted_data = {
            "company_details": self.extract_company_details(body),
            "contact_details": self.extract_contact_details(body),
            "products": self.extract_products(body),
            "commercial_terms": self.extract_commercial_terms(body),
            "confidence": confidence
        }

        result["extracted_data"] = extracted_data

        # Store supplier
        company_name = extracted_data["company_details"].get("company_name")
        if not company_name:
            result["status"] = "failed"
            result["message"] = "Could not extract company name"
            return result

        email_source = {
            "from": from_address,
            "subject": subject,
            "email_id": email_id,
            "date": email_data.get("date"),
            "has_attachments": email_data.get("has_attachments", False)
        }

        success, supplier_id_or_error = self.store_supplier(extracted_data, email_source)

        if success:
            result["status"] = "extracted_and_stored"
            result["supplier_id"] = supplier_id_or_error
            result["message"] = f"✅ Supplier extracted and stored: {supplier_id_or_error}"

            # Send notification to Solutions team
            self.send_supplier_notification(supplier_id_or_error, extracted_data)
        else:
            result["status"] = "duplicate_or_error"
            result["message"] = supplier_id_or_error

        return result

    def send_supplier_notification(self, supplier_id: str, extracted_data: Dict) -> bool:
        """
        Send notification email to Solutions team with supplier details summary
        """
        try:
            company_name = extracted_data["company_details"].get("company_name", "Unknown")
            contact_person = extracted_data["contact_details"].get("contact_person", "N/A")
            contact_email = extracted_data["contact_details"].get("email", "N/A")
            contact_phone = extracted_data["contact_details"].get("phone", "N/A")
            products = extracted_data["products"].get("categories", [])
            mentioned_products = extracted_data["products"].get("mentioned_products", [])
            confidence = extracted_data.get("confidence", 0)

            # Create email content
            subject = f"✅ New Supplier Added: {company_name}"

            # Format products section
            products_html = ""
            if mentioned_products:
                # Clean and categorize products
                clean_products = []
                for product in mentioned_products:
                    # Remove bullet points and extra characters
                    clean = product.strip().lstrip('-•◦').strip()
                    if len(clean) > 2 and clean not in clean_products:
                        clean_products.append(clean)

                if clean_products:
                    products_html = f"""
                    <h4 style="color: #1976D2;">📦 المنتجات والخدمات المقدمة:</h4>
                    <ul>
                        {''.join([f'<li>{product}</li>' for product in clean_products[:10]])}
                    </ul>
                    """

            # Check for distributor - pass product categories for better detection
            distributor_html = ""
            product_categories = extracted_data.get("products", {}).get("categories", [])
            body_text = str(extracted_data)  # Convert to string for text search
            distributor = self.detect_libya_distributor(body_text, product_categories)
            if distributor:
                distributor_html = f"""
                    <h4 style="color: #FF6B35;">🌍 الموزع الموصول إلى ليبيا:</h4>
                    <p style="background-color: #FFE8D6; padding: 8px; border-radius: 4px;">
                        <strong>{distributor}</strong>
                    </p>
                    """

            html_body = f"""
            <html>
            <body style="font-family: Arial, sans-serif; direction: rtl; text-align: right;">
                <h2 style="color: #2E7D32;">✅ مورد جديد تم إضافته</h2>

                <div style="border: 1px solid #ddd; padding: 15px; margin: 10px 0; background-color: #f9f9f9;">
                    <h3 style="color: #1976D2;">{company_name}</h3>

                    <p><strong>معرف المورد:</strong> {supplier_id}</p>

                    <h4 style="color: #1976D2;">👤 بيانات الاتصال:</h4>
                    <ul>
                        <li><strong>الشخص:</strong> {contact_person}</li>
                        <li><strong>البريد الإلكتروني:</strong> {contact_email}</li>
                        <li><strong>الهاتف:</strong> {contact_phone}</li>
                    </ul>

                    {products_html}

                    {distributor_html}

                    <h4 style="color: #1976D2;">🏷️ الفئات:</h4>
                    <p>{", ".join(products) if products else "لم يتم تحديد الفئات"}</p>

                    <h4 style="color: #1976D2;">📊 درجة الثقة:</h4>
                    <p style="font-size: 18px; font-weight: bold; color: #2E7D32;">{confidence:.0f}%</p>

                    <p style="margin-top: 15px; font-size: 12px; color: #666;">
                        <em>تم التخزين تلقائياً في قاعدة البيانات</em>
                    </p>
                </div>
            </body>
            </html>
            """

            # Email credentials
            sender_email = "alwadidevices@gmail.com"
            sender_password = "gbbh vvuu khso dzzd"
            recipient_email = "solutions@alwadi.ly"

            # Create message
            msg = MIMEMultipart('alternative')
            msg['Subject'] = subject
            msg['From'] = sender_email
            msg['To'] = recipient_email

            # Attach HTML content
            msg.attach(MIMEText(html_body, 'html', 'utf-8'))

            # Send email
            server = smtplib.SMTP_SSL('smtp.gmail.com', 465)
            server.login(sender_email, sender_password)
            server.send_message(msg)
            server.quit()

            return True
        except Exception as e:
            print(f"⚠️ Failed to send notification: {str(e)}")
            return False

    def get_supplier(self, supplier_id: str) -> Optional[Dict]:
        """Retrieve supplier information by ID"""
        supplier_file = os.path.join(self.suppliers_path, f"{supplier_id}.json")
        if os.path.exists(supplier_file):
            with open(supplier_file, 'r', encoding='utf-8') as f:
                return json.load(f)
        return None

    def search_suppliers_by_product(self, product_category: str) -> List[Dict]:
        """Find all suppliers offering a specific product category"""
        supplier_ids = self.suppliers_index.get("by_product", {}).get(product_category.lower(), [])
        suppliers = []
        for sup_id in supplier_ids:
            supplier = self.get_supplier(sup_id)
            if supplier:
                suppliers.append(supplier)
        return suppliers

    def search_suppliers_by_name(self, company_name: str) -> Optional[Dict]:
        """Find supplier by company name"""
        supplier_id = self.suppliers_index.get("by_company", {}).get(company_name.lower().strip())
        if supplier_id:
            return self.get_supplier(supplier_id)
        return None

    def get_all_suppliers(self) -> List[Dict]:
        """Retrieve all stored suppliers"""
        suppliers = []
        for supplier_id in self.suppliers_index.get("suppliers", []):
            supplier = self.get_supplier(supplier_id)
            if supplier:
                suppliers.append(supplier)
        return suppliers

    def get_extraction_statistics(self) -> Dict[str, Any]:
        """Get extraction statistics"""
        return {
            "total_suppliers": len(self.suppliers_index.get("suppliers", [])),
            "total_extracted": self.extraction_log.get("extraction_count", 0),
            "duplicates_detected": self.extraction_log.get("duplicate_detected", 0),
            "last_extraction": self.extraction_log.get("last_extraction"),
            "suppliers_by_product": len(self.suppliers_index.get("by_product", {})),
            "product_categories": list(self.suppliers_index.get("by_product", {}).keys())
        }


if __name__ == "__main__":
    # Demo: Process a sample supplier email
    extractor = AutomaticSupplierExtractor()

    # Sample email (like the Turkey UPS supplier)
    sample_email = {
        "subject": "Fw: TURKEY- SUPPLIER OFFER - RACK SYSTEMS, UPS SYSTEMS, STRUCTURED CABLING",
        "body": """
        Company Name: Alemsan Elektronik
        Headquarters: Istanbul, Turkey
        Website: www.alemsan.com

        Contact Person: Feras Wajih Sandouka
        Position: International Marketing Manager
        Email: feraswajih@hotmail.com
        Phone: +90 (216) 4292479
        Mobile: +90 (555) 7283752
        Skype: feras.wajih

        PRODUCTS & SERVICES OFFERED:
        - 19-inch indoor/outdoor server cabinets
        - Raised floor systems
        - Mechanical & custom-designed enclosures
        - Power distribution units (PDUs)
        - UPS (Uninterruptible Power Supply) devices
        - Power backup solutions
        - Fiber & Copper network cabling solutions
        - Structured cabling systems
        - Fiber optics high-density termination modules
        """,
        "from_address": "i.albeerish@alwadi.ly",
        "date": datetime.utcnow().isoformat(),
        "email_id": "sample_2026_alemsan",
        "has_attachments": True
    }

    result = extractor.process_email(sample_email)
    print("\n=== AUTOMATIC SUPPLIER EXTRACTION RESULT ===")
    print(json.dumps(result, indent=2, ensure_ascii=False))

    # Show statistics
    stats = extractor.get_extraction_statistics()
    print("\n=== EXTRACTION STATISTICS ===")
    print(json.dumps(stats, indent=2, ensure_ascii=False))
