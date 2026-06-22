#!/bin/bash

# ============================================================================
# DocVault Enterprise - Automatic Installation Script
# يقوم بالتثبيت التلقائي على Linux
# ============================================================================

set -e

# الألوان
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# ============================================================================
# دوال المساعدة
# ============================================================================

print_header() {
    echo -e "\n${GREEN}════════════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}$1${NC}"
    echo -e "${GREEN}════════════════════════════════════════════════════════════${NC}\n"
}

print_info() {
    echo -e "${YELLOW}ℹ️  $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# ============================================================================
# التحقق من الأذونات
# ============================================================================

print_header "التحقق من الأذونات والمتطلبات"

if [[ $EUID -ne 0 ]]; then
    print_error "هذا السكريبت يتطلب أذونات المسؤول (sudo)"
    exit 1
fi

print_success "يتم التشغيل كمسؤول"

# ============================================================================
# تحديث نظام التشغيل
# ============================================================================

print_header "تحديث نظام التشغيل"

if command -v apt &> /dev/null; then
    print_info "اكتشفت Ubuntu/Debian"
    apt update
    apt upgrade -y

    # تثبيت المتطلبات الأساسية
    apt install -y \
        curl \
        wget \
        gnupg \
        git \
        build-essential \
        libssl-dev

    print_success "تم تثبيت المتطلبات الأساسية"

elif command -v yum &> /dev/null; then
    print_info "اكتشفت RHEL/CentOS"
    yum update -y

    yum install -y \
        curl \
        wget \
        gnupg \
        git \
        gcc \
        openssl-devel

    print_success "تم تثبيت المتطلبات الأساسية"
else
    print_error "نظام غير مدعوم. استخدم Ubuntu أو RHEL"
    exit 1
fi

# ============================================================================
# تثبيت .NET 8 SDK
# ============================================================================

print_header "تثبيت .NET 8 SDK"

if ! command -v dotnet &> /dev/null; then
    print_info "تثبيت .NET 8..."

    if command -v apt &> /dev/null; then
        wget https://dot.net/dotnet-release-linux.gpg.key
        apt-key add dotnet-release-linux.gpg.key
        apt-get install -y apt-transport-https
        add-apt-repository "deb [arch=amd64] https://apt.releases.hashicorp.com $(lsb_release -cs) main"
        apt update
        apt install -y dotnet-sdk-8.0

    elif command -v yum &> /dev/null; then
        rpm -Uvh https://packages.microsoft.com/config/rhel/9/packages-microsoft-prod.rpm
        yum install -y dotnet-sdk-8.0
    fi

    print_success "تم تثبيت .NET 8"
else
    DOTNET_VERSION=$(dotnet --version)
    print_success ".NET مثبت بالفعل: $DOTNET_VERSION"
fi

# ============================================================================
# تثبيت Docker و Docker Compose
# ============================================================================

print_header "تثبيت Docker و Docker Compose"

if ! command -v docker &> /dev/null; then
    print_info "تثبيت Docker..."

    curl -fsSL https://get.docker.com -o get-docker.sh
    sh get-docker.sh
    rm get-docker.sh

    # إضافة المستخدم الحالي إلى مجموعة docker
    usermod -aG docker $SUDO_USER

    print_success "تم تثبيت Docker"
else
    print_success "Docker مثبت بالفعل"
fi

# تثبيت Docker Compose
if ! command -v docker-compose &> /dev/null; then
    print_info "تثبيت Docker Compose..."

    curl -L "https://github.com/docker/compose/releases/download/v2.20.0/docker-compose-$(uname -s)-$(uname -m)" \
        -o /usr/local/bin/docker-compose
    chmod +x /usr/local/bin/docker-compose

    print_success "تم تثبيت Docker Compose"
else
    print_success "Docker Compose مثبت بالفعل"
fi

# ============================================================================
# تثبيت Nginx
# ============================================================================

print_header "تثبيت Nginx"

if ! command -v nginx &> /dev/null; then
    print_info "تثبيت Nginx..."

    if command -v apt &> /dev/null; then
        apt install -y nginx
    elif command -v yum &> /dev/null; then
        yum install -y nginx
    fi

    systemctl enable nginx
    systemctl start nginx

    print_success "تم تثبيت Nginx"
else
    print_success "Nginx مثبت بالفعل"
fi

# ============================================================================
# تنزيل مشروع DocVault
# ============================================================================

print_header "تنزيل مشروع DocVault"

INSTALL_DIR="/opt/docvault"

if [ -d "$INSTALL_DIR" ]; then
    print_info "المجلد موجود بالفعل"
    read -p "هل تريد المتابعة؟ (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
else
    mkdir -p $INSTALL_DIR
fi

cd $INSTALL_DIR

if [ -d ".git" ]; then
    print_info "تحديث المشروع..."
    git pull
else
    print_info "استنساخ المشروع..."
    git clone https://github.com/alwadi/docvault.git . 2>/dev/null || \
    git clone https://github.com/alwadi/docvault .
fi

print_success "تم تنزيل المشروع"

# ============================================================================
# بناء المشروع
# ============================================================================

print_header "بناء المشروع"

dotnet restore
dotnet publish -c Release -o /var/www/docvault

print_success "تم بناء المشروع"

# ============================================================================
# إنشاء خدمة Systemd
# ============================================================================

print_header "إعداد خدمة Systemd"

tee /etc/systemd/system/docvault.service > /dev/null <<EOF
[Unit]
Description=DocVault Enterprise Document Management System
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=/var/www/docvault
ExecStart=/usr/bin/dotnet /var/www/docvault/DocVault_Demo_Complete.dll
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable docvault
systemctl start docvault

print_success "تم إنشاء خدمة Systemd"

# ============================================================================
# إعدادات Nginx
# ============================================================================

print_header "إعداد Nginx كـ Reverse Proxy"

tee /etc/nginx/sites-available/docvault > /dev/null <<'EOF'
upstream docvault {
    server 127.0.0.1:5000;
}

server {
    listen 80;
    server_name _;

    client_max_body_size 500M;

    location / {
        proxy_pass http://docvault;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
EOF

ln -sf /etc/nginx/sites-available/docvault /etc/nginx/sites-enabled/docvault
rm -f /etc/nginx/sites-enabled/default

nginx -t
systemctl restart nginx

print_success "تم إعداد Nginx"

# ============================================================================
# تفعيل SSL (Let's Encrypt)
# ============================================================================

print_header "تثبيت Certbot لـ Let's Encrypt (اختياري)"

read -p "هل تريد تثبيت SSL مع Let's Encrypt؟ (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then

    if command -v apt &> /dev/null; then
        apt install -y certbot python3-certbot-nginx
    elif command -v yum &> /dev/null; then
        yum install -y certbot python3-certbot-nginx
    fi

    read -p "أدخل اسم النطاق الخاص بك: " domain_name

    if [ -n "$domain_name" ]; then
        certbot --nginx -d $domain_name
        systemctl enable certbot.timer
        print_success "تم تثبيت SSL"
    fi
fi

# ============================================================================
# إعداد جدار الحماية
# ============================================================================

print_header "إعداد جدار الحماية (UFW)"

read -p "هل تريد تفعيل UFW؟ (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then

    apt install -y ufw

    ufw allow 22/tcp      # SSH
    ufw allow 80/tcp      # HTTP
    ufw allow 443/tcp     # HTTPS
    ufw --force enable

    print_success "تم تفعيل UFW"
fi

# ============================================================================
# الملخص النهائي
# ============================================================================

print_header "🎉 تم التثبيت بنجاح!"

echo "📊 معلومات التثبيت:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📁 مجلد التثبيت:        $INSTALL_DIR"
echo "🌐 رابط التطبيق:        http://localhost"
echo "🔧 ملف الخدمة:          /etc/systemd/system/docvault.service"
echo "📝 السجلات:             journalctl -u docvault -f"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo -e "\n${GREEN}الأوامر المفيدة:${NC}"
echo "  بدء الخدمة:        sudo systemctl start docvault"
echo "  إيقاف الخدمة:      sudo systemctl stop docvault"
echo "  إعادة تشغيل:       sudo systemctl restart docvault"
echo "  حالة الخدمة:       sudo systemctl status docvault"
echo "  السجلات الفورية:   sudo journalctl -u docvault -f"

echo -e "\n${GREEN}الخطوات التالية:${NC}"
echo "  1. افتح المتصفح على: http://$(hostname -I | awk '{print $1}')"
echo "  2. قم بتسجيل الدخول"
echo "  3. ابدأ باستخدام النظام"

echo -e "\n${GREEN}للمساعدة:${NC}"
echo "  📧 البريد: support@alwadi.ly"
echo "  📚 التوثيق: https://docs.alwadi.ly"

echo -e "\n"
