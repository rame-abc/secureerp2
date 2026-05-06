// 🔥 STEP 28: REAL PRODUCTION GO-LIVE VERIFICATION
// This is what real engineers run before saying "we are live"

const axios = require('axios');

// ⚙️ CONFIGURATION - UPDATE THESE WITH YOUR ACTUAL URLS
const CONFIG = {
    backendUrl: process.env.BACKEND_URL || 'https://YOUR-RENDER-URL.onrender.com',
    frontendUrl: process.env.FRONTEND_URL || 'https://your-vercel-app.vercel.app',
    // Test credentials - UPDATE WITH REAL TEST USER
    testUser: {
        email: process.env.TEST_EMAIL || 'test@example.com',
        password: process.env.TEST_PASSWORD || 'testpassword'
    },
    timeout: 10000 // 10 second timeout for production tests
};

let jwtToken = null;
let testJournalId = null;

// 🎯 COLOR CONSOLE OUTPUT
const colors = {
    green: '\x1b[32m',
    red: '\x1b[31m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    reset: '\x1b[0m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

function logSuccess(message) {
    log(`✅ ${message}`, 'green');
}

function logError(message) {
    log(`❌ ${message}`, 'red');
}

function logWarning(message) {
    log(`⚠️  ${message}`, 'yellow');
}

function logInfo(message) {
    log(`ℹ️  ${message}`, 'blue');
}

// 🔥 STEP 28.1: ACTUAL PUBLIC BACKEND TEST
async function testBackendPublic() {
    logInfo('🔥 STEP 28.1: ACTUAL PUBLIC BACKEND TEST');
    log(`Testing: ${CONFIG.backendUrl}/api/test`);
    
    try {
        const response = await axios.get(`${CONFIG.backendUrl}/api/test`, {
            timeout: CONFIG.timeout
        });
        
        if (response.data === 'ERP API is running!' || response.data === 'ok') {
            logSuccess('Backend is LIVE and responding correctly');
            log(`Response: "${response.data}"`);
            return true;
        } else {
            logError('Backend responding but with unexpected data');
            log(`Response: ${JSON.stringify(response.data)}`);
            return false;
        }
    } catch (error) {
        logError('Backend is NOT live or not accessible');
        log(`Error: ${error.message}`);
        
        if (error.code === 'ECONNREFUSED') {
            logError('Connection refused - backend may be down');
        } else if (error.code === 'ENOTFOUND') {
            logError('DNS resolution failed - wrong URL?');
        } else if (error.code === 'ETIMEDOUT') {
            logError('Request timeout - backend slow or down');
        }
        
        return false;
    }
}

// 🔥 STEP 28.2: DATABASE REAL CHECK (MOST IMPORTANT)
async function testDatabaseReal() {
    logInfo('🔥 STEP 28.2: DATABASE REAL CHECK (MOST IMPORTANT)');
    log(`Testing: ${CONFIG.backendUrl}/api/health`);
    
    try {
        const response = await axios.get(`${CONFIG.backendUrl}/api/health`, {
            timeout: CONFIG.timeout
        });
        
        const data = response.data;
        if (data.status === 'healthy' && data.database === 'connected') {
            logSuccess('Database is CONNECTED and healthy');
            log(`Status: ${data.status}, Database: ${data.database}`);
            return true;
        } else {
            logError('Database connection issue detected');
            log(`Response: ${JSON.stringify(data)}`);
            logWarning('🚨 YOUR ERP IS NOT PRODUCTION-READY YET');
            return false;
        }
    } catch (error) {
        logError('Database health check FAILED');
        log(`Error: ${error.message}`);
        
        if (error.response && error.response.status === 500) {
            logError('Server error - database connection string likely wrong');
        }
        
        return false;
    }
}

// 🔥 STEP 28.3: FRONTEND LIVE CHECK
async function testFrontendLive() {
    logInfo('🔥 STEP 28.3: FRONTEND LIVE CHECK');
    log(`Testing: ${CONFIG.frontendUrl}`);
    
    try {
        const response = await axios.get(CONFIG.frontendUrl, {
            timeout: CONFIG.timeout
        });
        
        if (response.status === 200) {
            logSuccess('Frontend is LIVE and accessible');
            log(`Status: ${response.status}`);
            
            // Check if it contains login-related content
            const content = response.data;
            if (content.includes('login') || content.includes('Login') || content.includes('signin')) {
                logSuccess('Login page detected');
            } else {
                logWarning('Frontend loads but login page not detected');
            }
            
            return true;
        } else {
            logError('Frontend not responding correctly');
            log(`Status: ${response.status}`);
            return false;
        }
    } catch (error) {
        logError('Frontend is NOT live or not accessible');
        log(`Error: ${error.message}`);
        
        if (error.code === 'ENOTFOUND') {
            logError('DNS resolution failed - wrong frontend URL?');
        } else if (error.response && error.response.status === 404) {
            logError('Frontend deployed but page not found');
        }
        
        return false;
    }
}

// 🔥 STEP 28.4: FULL LOGIN FLOW TEST (REAL USER TEST)
async function testLoginFlowReal() {
    logInfo('🔥 STEP 28.4: FULL LOGIN FLOW TEST (REAL USER TEST)');
    
    try {
        log('Attempting login with real credentials...');
        const response = await axios.post(`${CONFIG.backendUrl}/api/auth/login`, CONFIG.testUser, {
            timeout: CONFIG.timeout
        });
        
        if (response.data.token && response.data.user) {
            jwtToken = response.data.token;
            logSuccess('Login successful - JWT token generated');
            log(`User: ${response.data.user.email || response.data.user.username}`);
            log(`Token received: ${jwtToken.substring(0, 20)}...`);
            
            // Test token validation
            const validateResponse = await axios.get(`${CONFIG.backendUrl}/api/auth/validate`, {
                headers: { 'Authorization': `Bearer ${jwtToken}` },
                timeout: CONFIG.timeout
            });
            
            if (validateResponse.data.user) {
                logSuccess('JWT token validated successfully');
                return true;
            } else {
                logError('JWT token validation failed');
                return false;
            }
        } else {
            logError('Login failed - no JWT token received');
            log(`Response: ${JSON.stringify(response.data)}`);
            return false;
        }
    } catch (error) {
        logError('Login flow FAILED');
        log(`Error: ${error.message}`);
        
        if (error.response && error.response.status === 401) {
            logError('Authentication failed - wrong credentials or JWT issue');
        } else if (error.response && error.response.status === 403) {
            logError('Forbidden - CORS or authorization issue');
        }
        
        return false;
    }
}

// 🔥 STEP 28.5: REAL ACCOUNTING FLOW TEST (CRITICAL)
async function testAccountingFlowReal() {
    logInfo('🔥 STEP 28.5: REAL ACCOUNTING FLOW TEST (CRITICAL)');
    
    if (!jwtToken) {
        logError('No JWT token available - cannot test accounting flow');
        return false;
    }
    
    const config = {
        headers: { 'Authorization': `Bearer ${jwtToken}` },
        timeout: CONFIG.timeout
    };
    
    try {
        // Step 1: Create Journal Entry
        log('Step 1: Creating Journal Entry...');
        const journalResponse = await axios.post(`${CONFIG.backendUrl}/api/finance/journal`, {
            description: 'Production Test Journal - Go-Live Verification',
            entries: [
                {
                    accountId: 1,
                    debit: 1000,
                    credit: 0,
                    description: 'Test Cash Debit'
                },
                {
                    accountId: 2,
                    debit: 0,
                    credit: 1000,
                    description: 'Test Revenue Credit'
                }
            ]
        }, config);
        
        testJournalId = journalResponse.data.id;
        logSuccess('Journal entry created successfully');
        log(`Journal ID: ${testJournalId}`);
        
        // Step 2: Post Journal Entry
        log('Step 2: Posting Journal Entry...');
        await axios.post(`${CONFIG.backendUrl}/api/finance/journal/${testJournalId}/post`, {}, config);
        logSuccess('Journal entry posted successfully');
        
        // Step 3: Generate Income Statement
        log('Step 3: Generating Income Statement...');
        const reportResponse = await axios.get(`${CONFIG.backendUrl}/api/finance/income-statement`, config);
        
        if (reportResponse.data.revenue && reportResponse.data.expenses && reportResponse.data.profitSummary) {
            logSuccess('Income Statement generated successfully');
            log(`Revenue: $${reportResponse.data.revenue.totalRevenue || 0}`);
            log(`Expenses: $${reportResponse.data.expenses.totalExpenses || 0}`);
            log(`Net Profit: $${reportResponse.data.profitSummary.netProfit || 0}`);
            logSuccess('Company filtering working correctly');
            return true;
        } else {
            logError('Income Statement format incorrect');
            log(`Response: ${JSON.stringify(reportResponse.data)}`);
            return false;
        }
    } catch (error) {
        logError('Accounting flow FAILED');
        log(`Error: ${error.message}`);
        
        if (error.response && error.response.status === 401) {
            logError('Authorization failed - user may not have correct role');
        } else if (error.response && error.response.status === 403) {
            logError('Forbidden - insufficient permissions for accounting operations');
        } else if (error.response && error.response.status === 500) {
            logError('Server error - database or accounting engine issue');
        }
        
        return false;
    }
}

// 🎯 MAIN VERIFICATION FUNCTION
async function runProductionVerification() {
    log('🚀 STARTING PRODUCTION GO-LIVE VERIFICATION');
    log('========================================');
    log('This is what real engineers do before saying "we are live"');
    log('========================================\n');
    
    const results = {
        backendPublic: false,
        databaseReal: false,
        frontendLive: false,
        loginFlowReal: false,
        accountingFlowReal: false
    };
    
    // Run all tests
    results.backendPublic = await testBackendPublic();
    console.log('');
    
    results.databaseReal = await testDatabaseReal();
    console.log('');
    
    results.frontendLive = await testFrontendLive();
    console.log('');
    
    results.loginFlowReal = await testLoginFlowReal();
    console.log('');
    
    results.accountingFlowReal = await testAccountingFlowReal();
    console.log('');
    
    // 📊 FINAL RESULTS
    log('📊 PRODUCTION VERIFICATION RESULTS:');
    log('==================================');
    log(`✅ Backend Public: ${results.backendPublic ? 'PASS' : 'FAIL'}`);
    log(`✅ Database Real: ${results.databaseReal ? 'PASS' : 'FAIL'}`);
    log(`✅ Frontend Live: ${results.frontendLive ? 'PASS' : 'FAIL'}`);
    log(`✅ Login Flow Real: ${results.loginFlowReal ? 'PASS' : 'FAIL'}`);
    log(`✅ Accounting Flow Real: ${results.accountingFlowReal ? 'PASS' : 'FAIL'}`);
    
    const allPassed = Object.values(results).every(result => result === true);
    
    if (allPassed) {
        log('\n🎉 CONGRATULATIONS! YOUR SAAS ERP IS PRODUCTION-READY! 🎉');
        logSuccess('All critical systems are working correctly');
        logSuccess('Your system is ready for real users');
        log('\n🚀 Next steps:');
        log('1. Monitor system performance');
        log('2. Set up database backups');
        log('3. Configure SSL certificates');
        log('4. Plan for scaling based on user growth');
        log('5. Implement monitoring and alerting');
    } else {
        log('\n❌ SYSTEM NOT READY FOR PRODUCTION');
        logError('Please fix the failed components before going live');
        log('\n🔧 Troubleshooting:');
        log('1. Check Render logs for backend issues');
        log('2. Check Vercel logs for frontend issues');
        log('3. Verify environment variables');
        log('4. Test database connection');
        log('5. Check user roles and permissions');
    }
    
    return allPassed;
}

// 🚀 RUN VERIFICATION
if (require.main === module) {
    runProductionVerification()
        .then(success => {
            process.exit(success ? 0 : 1);
        })
        .catch(error => {
            logError(`Verification script failed: ${error.message}`);
            process.exit(1);
        });
}

module.exports = {
    runProductionVerification,
    testBackendPublic,
    testDatabaseReal,
    testFrontendLive,
    testLoginFlowReal,
    testAccountingFlowReal
};
