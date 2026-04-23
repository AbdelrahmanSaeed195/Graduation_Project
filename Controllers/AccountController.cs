using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using projectweb.ViewModel;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace projectweb.Controllers
{
    public class AccountController : Controller
    {
        public UserManager<IdentityUser> UserManager { get; }
        public SignInManager<IdentityUser> SignInManager { get; }

        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public async Task< IActionResult> Register(RegisterViewModel Aaccount)
        {
            if (ModelState.IsValid == true)
            {
                IdentityUser user=new IdentityUser();

                user.UserName = Aaccount.FullName;
                user.Email = Aaccount.Email;
                user.PhoneNumber = Aaccount.PhoneNumber;
                

              var resalt=  await UserManager.CreateAsync(user, Aaccount.Password);
                if (resalt.Succeeded)
                {
                    await SignInManager.SignInAsync(user, false);
                    return RedirectToAction("Index", "Home");
                }
                else 
                {
                    foreach(var error in resalt.Errors)
                    { 
                        ModelState.AddModelError("",error.Description);
                    }
                }
            }
            return View(Aaccount);
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel login)
        {
            if (ModelState.IsValid == true)
            {
                IdentityUser user = await UserManager.FindByEmailAsync(login.Email);
                if (user != null)
                {
                  var singn= await SignInManager.PasswordSignInAsync(user,login.Password,login.IsPrisist ,false);
                    if (singn.Succeeded)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid Email And Passowrd");

                    }
                }
                else
                {
                    ModelState.AddModelError("","Invalid Email And Passowrd");

                }

            }
            return View(login);
        }
        public async Task<IActionResult> Logout()
        {
            await SignInManager.SignOutAsync();
            return RedirectToAction("Login","Account");
        }
        [HttpGet]
        public IActionResult AddAdmin()
        {
            return View("Register");
        }
        [HttpPost]
        public async Task<IActionResult> AddAdmin(RegisterViewModel Aaccount)
        {
            if (ModelState.IsValid == true)
            {
                IdentityUser user = new IdentityUser();

                user.UserName = Aaccount.FullName;
                user.Email = Aaccount.Email;
                user.PhoneNumber = Aaccount.PhoneNumber;


                var resalt = await UserManager.CreateAsync(user, Aaccount.Password);
                if (resalt.Succeeded)
                {
                    await UserManager.AddToRoleAsync(user, "Admin");
                    await SignInManager.SignInAsync(user, false);
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    foreach (var error in resalt.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
            }
            return View("Register", Aaccount);
        }
    }
}
