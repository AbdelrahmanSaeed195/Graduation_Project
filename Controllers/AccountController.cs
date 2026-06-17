using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using projectweb.Services;
using projectweb.ViewModel;

namespace projectweb.Controllers
{
    public class AccountController : Controller
    {
        public UserManager<IdentityUser> UserManager { get; }
        public SignInManager<IdentityUser> SignInManager { get; }
        public EmailService EmailService { get; }

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            EmailService emailService)
        {
            UserManager = userManager;
            SignInManager = signInManager;
            EmailService = emailService;
        }

        // ================= OTP STORE =================
        private static Dictionary<string, string> otpStore = new();

        // =========================================================
        // REGISTER
        // =========================================================
        //[HttpGet]
        //public IActionResult Register() => View();

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Register(RegisterViewModel account)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var user = new IdentityUser
        //        {
        //            UserName = account.Email,
        //            Email = account.Email,
        //            PhoneNumber = account.PhoneNumber
        //        };

        //        var result = await UserManager.CreateAsync(user, account.Password);

        //        if (result.Succeeded)
        //        {
        //            await SignInManager.SignInAsync(user, false);
        //            return RedirectToAction("Index", "Home");
        //        }

        //        foreach (var error in result.Errors)
        //            ModelState.AddModelError("", error.Description);
        //    }

        //    return View(account);
        //}

        // =========================================================
        // LOGIN
        // =========================================================
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel login)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByEmailAsync(login.Email);

                if (user != null)
                {
                    var result = await SignInManager.PasswordSignInAsync(
                        user,
                        login.Password,
                        login.IsPrisist,
                        false
                    );

                    if (result.Succeeded)
                        return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid Email Or Password");
            }

            return View(login);
        }

        // =========================================================
        // LOGOUT
        // =========================================================
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await SignInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // =========================================================
        // ADD ADMIN
        // =========================================================
        [HttpGet]
        public IActionResult AddAdmin() => View("Register");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(RegisterViewModel account)
        {
            if (!ModelState.IsValid)
                return View("Register", account);

            // Check Email Exists
            var existingUser = await UserManager.FindByEmailAsync(account.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError("", "Email already exists");
                return View("Register", account);
            }

            // Create User
            var user = new IdentityUser
            {
                UserName = account.Email,
                Email = account.Email,
                PhoneNumber = account.PhoneNumber,
                EmailConfirmed = true
            };

            var createResult = await UserManager.CreateAsync(
                user,
                account.Password
            );

            // Check Create
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View("Register", account);
            }

            // Add Admin Role
            var roleResult = await UserManager.AddToRoleAsync(
                user,
                "Admin"
            );

            // Check Role
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View("Register", account);
            }

            TempData["Success"] = "Admin Created Successfully";

            return RedirectToAction("Login");
        }

        // =========================================================
        // CHANGE PASSWORD
        // =========================================================
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.GetUserAsync(User);

                if (user != null)
                {
                    var result = await UserManager.ChangePasswordAsync(
                        user,
                        model.CurrentPassword,
                        model.NewPassword
                    );

                    if (result.Succeeded)
                    {
                        await SignInManager.RefreshSignInAsync(user);
                        return RedirectToAction("Index", "Home");
                    }

                    foreach (var error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        // =========================================================
        // FORGOT PASSWORD (OTP STEP 1)
        // =========================================================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByEmailAsync(model.Email);

                if (user != null)
                {
                    // 1. Generate OTP
                    Random random = new Random();
                    string otp = random.Next(100000, 999999).ToString();

                    // 2. Save OTP
                    otpStore[model.Email] = otp;

                    // 3. Send Email
                    await EmailService.SendEmailAsync(
                        model.Email,
                        "Reset OTP",
                        $"<h2>Your OTP Code:</h2><h1>{otp}</h1>"
                    );

                    TempData["Email"] = model.Email;

                    return RedirectToAction("VerifyOtp");
                }

                ModelState.AddModelError("", "User Not Found");
            }

            return View(model);
        }

        // =========================================================
        // VERIFY OTP (STEP 2)
        // =========================================================
        [HttpGet]
        public IActionResult VerifyOtp() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyOtp(string otp)
        {
            string email = TempData["Email"]?.ToString();

            if (email == null)
                return RedirectToAction("ForgotPassword");

            if (otpStore.ContainsKey(email) && otpStore[email] == otp)
            {
                TempData["ResetEmail"] = email;
                return RedirectToAction("ResetPassword");
            }

            ModelState.AddModelError("", "Invalid OTP");
            return View();
        }

        // =========================================================
        // RESET PASSWORD (STEP 3)
        // =========================================================
        public IActionResult ResetPassword()
        {
            var model = new ResetPasswordViewModel
            {
                Email = TempData["ResetEmail"]?.ToString()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await UserManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User Not Found");
                return View(model);
            }

            // 🔥 تغيير الباسورد مباشرة
            user.PasswordHash = UserManager.PasswordHasher.HashPassword(user, model.NewPassword);

            var result = await UserManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                otpStore.Remove(model.Email); // حذف OTP
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }
    }
}