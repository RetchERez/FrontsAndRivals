// Fronts & Rivals: "The Office" contact on the phone (iFruitAddon2), opens the Office menu.
using iFruitAddon2;

namespace FrontsAndRivals.UI
{
    public sealed class PhoneContact
    {
        private readonly CustomiFruit _phone = new CustomiFruit();

        public PhoneContact(OfficeMenu menu)
        {
            var office = new iFruitContact("The Office")
            {
                DialTimeout = 1500,     // ms before answering
                Active = true,
                Icon = ContactIcon.Blank
            };
            office.Answered += contact =>
            {
                _phone.Close(500);      // close the phone so it doesn't steal menu input
                menu.Open();
            };
            _phone.Contacts.Add(office);
        }

        /// <summary>Call every tick.</summary>
        public void Update() { _phone.Update(); }
    }
}
