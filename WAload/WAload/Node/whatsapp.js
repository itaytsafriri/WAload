const { Client, LocalAuth } = require('whatsapp-web.js');
const qrcode = require('qrcode-terminal');
const path = require('path');
const fs = require('fs');

// Logging setup
const logStream = fs.createWriteStream(path.join(__dirname, 'node_debug.log'), { flags: 'a' });
const log = (message) => {
    const timestamp = new Date().toISOString();
    const formattedMessage = `[${timestamp}] ${message}\n`;
    logStream.write(formattedMessage);
    // Don't output to console.log as it interferes with JSON communication
};

log('=== WAload Node.js Service Starting ===');
log(`Node.js version: ${process.version}`);
log(`Script path: ${__dirname}`);

// Global state
let client = null;
let isMonitoring = false;
let selectedGroupId = null;
let lastQrTimestamp = 0;

// Error handling
process.on('uncaughtException', (err) => {
    log(`!!! UNCAUGHT EXCEPTION: ${err.message}`);
    log(`Stack: ${err.stack}`);
    process.exit(1);
});

process.on('unhandledRejection', (reason) => {
    const err = reason instanceof Error ? reason : new Error(JSON.stringify(reason));
    log(`!!! UNHANDLED REJECTION: ${err.message}`);
    log(`Stack: ${err.stack}`);
    process.exit(1);
});

function sendToHost(message) {
    console.log(JSON.stringify(message));
}

function createClient() {
    log('Creating WhatsApp client...');
    
    // Find Chromium executable
    let chromiumPath = path.join(__dirname, 'node_modules', 'puppeteer-core', '.local-chromium', 'win64-1045629', 'chrome-win', 'chrome.exe');
    
    if (!fs.existsSync(chromiumPath)) {
        const altPath = path.join(__dirname, 'chrome-win', 'chrome.exe');
        if (fs.existsSync(altPath)) {
            chromiumPath = altPath;
            log(`Using alternative Chromium path: ${chromiumPath}`);
        } else {
            log('Warning: Chromium not found, using system default');
            chromiumPath = undefined;
        }
    } else {
        log(`Using Chromium at: ${chromiumPath}`);
    }
    
    const client = new Client({
        puppeteer: {
            headless: true,
            executablePath: chromiumPath,
            args: [
                '--no-sandbox',
                '--disable-setuid-sandbox',
                '--disable-dev-shm-usage',
                '--disable-accelerated-2d-canvas',
                '--no-first-run',
                '--no-zygote',
                '--single-process',
                '--disable-gpu',
                '--disable-background-timer-throttling',
                '--disable-backgrounding-occluded-windows',
                '--disable-renderer-backgrounding'
            ],
            timeout: 60000
        },
        authStrategy: new LocalAuth({
            clientId: 'waload-client',
            dataPath: path.join(__dirname, '.wwebjs_auth')
        }),
        webVersionCache: {
            type: 'remote',
            remotePath: 'https://raw.githubusercontent.com/wppconnect-team/wa-version/main/html/2.2412.54.html',
        }
    });
    
    log('Client created successfully');
    return client;
}

async function waitForClientReady(client) {
    return new Promise(resolve => {
        if (client.info && client.info.wid) {
            log('Client already ready');
            resolve();
            return;
        }
        
        log('Waiting for client ready event...');
        client.once('ready', () => {
            log('Client ready event received');
            resolve();
        });
    });
}

async function dismissIntroPopup(page) {
    log('Checking for intro popup...');
    
    try {
        await page.waitForTimeout(2000);
        
        const selectors = [
            'button[data-testid="intro-text"]',
            'button:has-text("Got it")',
            'button:has-text("OK")',
            'button:has-text("Continue")',
            '[data-testid="intro-text"]',
            '.intro-text button'
        ];
        
        for (const selector of selectors) {
            try {
                const button = await page.$(selector);
                if (button) {
                    log(`Found button with selector: ${selector}`);
                    await button.click();
                    log('Intro popup dismissed');
                    return true;
                }
            } catch (e) {
                // Continue to next selector
            }
        }
        
        log('No intro popup found');
        return false;
        
    } catch (error) {
        log(`Error checking for intro popup: ${error.message}`);
        return false;
    }
}

async function getChatsWithRetry(client, maxAttempts = 5) {
    log('Getting chats with retry logic...');
    
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
        log(`Attempt ${attempt} of ${maxAttempts}`);
        
        try {
            // Wait for client to be ready
            await waitForClientReady(client);
            
            // Dismiss popups on first attempt
            if (attempt === 1 && client.pupPage) {
                await dismissIntroPopup(client.pupPage);
            }
            
            // Get chats with timeout
            const timeoutPromise = new Promise((_, reject) => {
                setTimeout(() => reject(new Error('Timeout')), 30000);
            });
            
            const getChatsPromise = client.getChats();
            const chats = await Promise.race([getChatsPromise, timeoutPromise]);
            
            if (chats && chats.length > 0) {
                log(`Successfully retrieved ${chats.length} chats`);
                return chats;
            }
            
            log(`Attempt ${attempt} returned no chats`);
            
        } catch (error) {
            log(`Error on attempt ${attempt}: ${error.message}`);
            
            if (attempt < maxAttempts) {
                log('Waiting 5 seconds before retry...');
                await new Promise(resolve => setTimeout(resolve, 5000));
            }
        }
    }
    
    log('All attempts to get chats failed');
    return [];
}

async function fetchAndSendGroups() {
    log('Fetching groups...');
    
    try {
        const chats = await getChatsWithRetry(client);
        
        const groups = chats
            .filter(chat => chat.isGroup)
            .map(chat => ({
                id: chat.id._serialized,
                name: chat.name
            }));
        
        log(`Found ${groups.length} groups`);
        sendToHost({ type: 'groups', groups });
        
    } catch (error) {
        log(`Error fetching groups: ${error.message}`);
        sendToHost({ type: 'groups', groups: [], error: error.message });
    }
}

function setupEventListeners() {
    // QR Code event
    client.on('qr', (qr) => {
        const now = Date.now();
        if (now - lastQrTimestamp > 5000) {
            log('QR code received');
            sendToHost({ type: 'qr', qr });
            lastQrTimestamp = now;
        }
    });
    
    // Ready event
    client.on('ready', async () => {
        log('Client is ready!');
        const userName = client.info.pushname;
        const userPlatform = client.info.platform;
        log(`Logged in as ${userName} on ${userPlatform}`);
        
        // Dismiss any intro popups
        if (client.pupPage) {
            await dismissIntroPopup(client.pupPage);
        }
        
        sendToHost({ type: 'status', connected: true });
        sendToHost({ type: 'userName', name: userName });
    });
    
    // Disconnected event
    client.on('disconnected', (reason) => {
        log(`Client disconnected: ${reason}`);
        isMonitoring = false;
        selectedGroupId = null;
        sendToHost({ type: 'status', connected: false });
        sendToHost({ type: 'monitoringStatus', monitoring: false });
    });
    
    // Message events
    client.on('message', async (msg) => {
        if (!isMonitoring || !selectedGroupId) return;
        
        try {
            const chat = await msg.getChat();
            if (chat.id._serialized === selectedGroupId && msg.hasMedia) {
                log('Media message received from monitored group');
                
                const media = await msg.downloadMedia();
                if (media) {
                    const contact = await msg.getContact();
                    
                    sendToHost({
                        type: 'media',
                        Media: {
                            Id: msg.id.id,
                            From: msg.from,
                            Author: msg.author,
                            Type: media.mimetype,
                            Timestamp: msg.timestamp,
                            Filename: media.filename,
                            Data: media.data,
                            Size: media.size,
                            SenderName: contact.pushname || contact.name || contact.number
                        }
                    });
                }
            }
        } catch (e) {
            log(`Error in message handler: ${e.message}`);
        }
    });
    
    client.on('message_create', async (msg) => {
        if (!isMonitoring || !selectedGroupId) return;
        
        try {
            const chat = await msg.getChat();
            if (chat.id._serialized === selectedGroupId && msg.hasMedia) {
                log('Media message received from monitored group (message_create)');
                
                const media = await msg.downloadMedia();
                if (media) {
                    const contact = await msg.getContact();
                    
                    sendToHost({
                        type: 'media',
                        Media: {
                            Id: msg.id.id,
                            From: msg.from,
                            Author: msg.author,
                            Type: media.mimetype,
                            Timestamp: msg.timestamp,
                            Filename: media.filename,
                            Data: media.data,
                            Size: media.size,
                            SenderName: contact.pushname || contact.name || contact.number
                        }
                    });
                }
            }
        } catch (e) {
            log(`Error in message_create handler: ${e.message}`);
        }
    });
}

async function initialize() {
    log('Initializing WhatsApp client...');
    
    try {
        client = createClient();
        setupEventListeners();
        
        log('Starting client initialization...');
        await client.initialize();
        log('Client initialization completed');
        
    } catch (error) {
        log(`!!!!!! CLIENT INITIALIZATION FAILED: ${error.message}`);
        log(`!!!!!! Stack: ${error.stack}`);
        sendToHost({ type: 'error', message: `Client initialization failed: ${error.message}` });
        process.exit(1);
    }
}

async function handleLogout() {
    log('Processing logout command');
    
    if (client) {
        try {
            log('Initiating logout...');
            await client.logout();
            log('Logout completed');
            
            await client.destroy();
            log('Client destroyed');
            
        } catch (error) {
            log(`Error during logout: ${error.message}`);
        }
    }
    
    log('Exiting process after logout');
    process.exit(0);
}

function handleCommand(data) {
    log(`Command received: ${data.toString()}`);
    
    let command;
    try {
        command = JSON.parse(data);
    } catch (e) {
        log(`Error parsing command: ${e.message}`);
        return;
    }
    
    switch (command.type) {
        case 'get_groups':
            log('Processing get_groups command');
            fetchAndSendGroups().catch(err => log(`Error in fetchAndSendGroups: ${err.message}`));
            break;
            
        case 'monitor_group':
            log(`Processing monitor_group command for group ID: ${command.groupId}`);
            selectedGroupId = command.groupId;
            isMonitoring = true;
            sendToHost({ type: 'monitoringStatus', monitoring: true });
            break;
            
        case 'stop_monitoring':
            log('Processing stop_monitoring command');
            isMonitoring = false;
            selectedGroupId = null;
            sendToHost({ type: 'monitoringStatus', monitoring: false });
            break;
            
        case 'logout':
            handleLogout().catch(err => log(`Error in logout: ${err.message}`));
            break;
            
        default:
            log(`Unknown command type: ${command.type}`);
    }
}

async function main() {
    log('Starting main function...');
    
    try {
        await initialize();
        log('Main initialization complete');
        
        // Send initial status
        sendToHost({ type: 'status', connected: false });
        
        // Set up command handling
        process.stdin.on('data', handleCommand);
        
        // Handle graceful shutdown
        process.on('SIGINT', async () => {
            log('SIGINT received, shutting down gracefully');
            if (client) {
                await handleLogout();
            } else {
                process.exit(0);
            }
        });
        
    } catch (error) {
        log(`Fatal error in main function: ${error.message}`);
        log(`Stack: ${error.stack}`);
        sendToHost({ type: 'error', message: `Fatal error: ${error.message}` });
        process.exit(1);
    }
}

main(); 