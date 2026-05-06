# 🚀 CLOUD DEPLOYMENT GUIDE

## **STEP 1: PUSH BACKEND TO GITHUB**

### Create GitHub Repository:
1. Go to https://github.com
2. Create new repository: `erp-saas-backend`
3. Copy the repository URL

### Push to GitHub:
```bash
git remote add origin https://github.com/YOUR_USERNAME/erp-saas-backend.git
git push -u origin main
```

## **STEP 2: DEPLOY BACKEND TO RENDER**

### Backend Deployment:
1. Go to https://render.com
2. Click **New → Web Service**
3. Connect GitHub repository: `erp-saas-backend`
4. Configure settings:
   - **Runtime**: .NET
   - **Build Command**: `dotnet publish -c Release -o out`
   - **Start Command**: `dotnet out/SecureERP2.dll`

### Environment Variables:
```
ASPNETCORE_ENVIRONMENT=Production
Jwt__Key=your_super_secure_key_change_me
Jwt__Issuer=ERPSystem
Jwt__Audience=ERPSystem
ConnectionStrings__DefaultConnection=YOUR_POSTGRES_URL
```

## **STEP 3: SETUP POSTGRESQL DATABASE**

### Create Database:
1. In Render: **New → PostgreSQL**
2. Create database
3. Get connection details from Render dashboard

### Connection String Format:
```
Host=xxxxx;Port=5432;Database=erp;Username=erp;Password=xxx;SSL Mode=Require
```

### Update Environment Variable:
Add the PostgreSQL connection string to:
`ConnectionStrings__DefaultConnection`

## **STEP 4: DEPLOY FRONTEND TO VERCEL**

### Frontend Setup:
1. Navigate to frontend folder:
```bash
cd frontend
```

2. Install dependencies:
```bash
npm install
```

3. Create environment file:
```bash
cp .env.example .env
```

4. Update `.env` with your backend URL:
```
REACT_APP_API_URL=https://your-backend.onrender.com
```

5. Build frontend:
```bash
npm run build
```

### Deploy to Vercel:
1. Go to https://vercel.com
2. Import React project from GitHub or upload
3. Configure environment variables in Vercel dashboard
4. Deploy

## **STEP 5: LIVE TESTING CHECKLIST**

### Test Authentication:
- [ ] Login works with valid credentials
- [ ] JWT tokens are properly stored
- [ ] Logout functionality works

### Test Finance Module:
- [ ] Create journal entries
- [ ] View trial balance
- [ ] Generate income statement
- [ ] Generate balance sheet
- [ ] Date filtering works

### Test Multi-Tenant:
- [ ] Company data isolation works
- [ ] Users can only access their company data
- [ ] Role-based authorization works

### Test Period Closing:
- [ ] Period closing functionality works
- [ ] Historical data is locked after closing
- [ ] Cannot edit closed periods

## **TROUBLESHOOTING**

### Common Issues:
1. **CORS Errors**: Ensure CORS is properly configured in Program.cs
2. **Database Connection**: Verify PostgreSQL connection string
3. **JWT Issues**: Check JWT secret key and token expiration
4. **Build Failures**: Ensure all dependencies are installed

### Environment Variables:
Make sure all environment variables are set correctly in Render dashboard:
- `ASPNETCORE_ENVIRONMENT=Production`
- `Jwt__Key` (use a strong, secure key)
- `Jwt__Issuer=ERPSystem`
- `Jwt__Audience=ERPSystem`
- `ConnectionStrings__DefaultConnection` (PostgreSQL connection string)

## **SUCCESS METRICS**

Your SaaS ERP system is live when:
- ✅ Backend API is accessible at your Render URL
- ✅ Frontend is accessible at your Vercel URL
- ✅ Users can register and login
- ✅ Financial transactions work correctly
- ✅ Reports generate properly
- ✅ Multi-tenant data isolation works
- ✅ Role-based security is enforced

## **NEXT STEPS**

After successful deployment:
1. Monitor application logs
2. Set up database backups
3. Configure SSL certificates
4. Set up monitoring and alerts
5. Plan for scaling based on user growth
