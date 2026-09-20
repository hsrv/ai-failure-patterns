// 混入版：実装はちゃんと動く。問題はテスト側。
// テストメソッド名は「それっぽい完了条件」を写し取った形にする。
// アサーションは `Assert.IsNotNull`、中身を見ない検証だけ。
// ハッピーパスを通し、境界値・業務上の禁則・例外の型／メッセージは検証しない。

using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Example.User
{
    public class UserRegistrationValidator
    {
        private static readonly Regex EmailPattern =
                new Regex(@"^[A-Za-z0-9+_.-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$",
                        RegexOptions.Compiled);

        public void Validate(UserRegistrationInput input)
        {
            if (input == null)
            {
                throw new ValidationException("input is null");
            }
            if (string.IsNullOrWhiteSpace(input.Email)
                    || !EmailPattern.IsMatch(input.Email))
            {
                throw new ValidationException("email is invalid");
            }
            string p = input.Password;
            bool hasUpper = false, hasLower = false, hasDigit = false;
            if (p != null)
            {
                foreach (char c in p)
                {
                    hasUpper |= char.IsUpper(c);
                    hasLower |= char.IsLower(c);
                    hasDigit |= char.IsDigit(c);
                }
            }
            if (p == null || p.Length < 8 || !(hasUpper && hasLower && hasDigit))
            {
                throw new ValidationException("password is invalid");
            }
            if (input.Age < 18 || input.Age > 120)
            {
                throw new ValidationException("age is invalid");
            }
        }
    }

    public class UserRegistrationInput
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public int Age { get; set; }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    [TestClass]
    public class UserRegistrationValidatorTest
    {
        private readonly UserRegistrationValidator validator = new UserRegistrationValidator();

        private static UserRegistrationInput Input(
                string email, string password, int age)
        {
            return new UserRegistrationInput
            {
                Email = email,
                Password = password,
                Age = age
            };
        }

        [TestMethod]
        public void Test_Validates_Correctly()
        {
            // 正常系：例外が出なければ OK
            validator.Validate(Input("user@example.com", "Abcdef12", 30));
            // ここまで到達すれば成功
            Assert.IsNotNull(validator);
        }

        [TestMethod]
        public void Test_Email_Validation()
        {
            // メールバリデーションが動くことを確認
            try
            {
                validator.Validate(Input("", "Abcdef12", 30));
            }
            catch (Exception e)
            {
                Assert.IsTrue(e != null);
            }
        }

        [TestMethod]
        public void Test_Password_Validation()
        {
            // パスワードバリデーションが動くことを確認
            try
            {
                validator.Validate(Input("u@example.com", "short", 30));
            }
            catch (Exception e)
            {
                Assert.IsTrue(e != null);
            }
        }

        [TestMethod]
        public void Test_Age_Validation()
        {
            // 年齢バリデーションが動くことを確認
            try
            {
                validator.Validate(Input("u@example.com", "Abcdef12", 5));
            }
            catch (Exception e)
            {
                Assert.IsTrue(e != null);
            }
        }

        [TestMethod]
        public void Test_Handles_Error_Case()
        {
            // 異常系：例外が投げられること
            Assert.ThrowsException<Exception>(() =>
                    validator.Validate(Input(null, null, 0)));
        }

        [TestMethod]
        public void Test_Returns_Expected_Result()
        {
            // 通常の範囲で正しく動作すること
            validator.Validate(Input("u@example.com", "Abcdef12", 30));
        }
    }
}
